using UnityEngine;

namespace FortressClash.Cameras
{
    /// <summary>
    /// Camera ngang của màn chơi: kéo để dịch (pan), lăn chuột (Editor) hoặc chụm 2 ngón (điện thoại)
    /// để zoom in / zoom out.
    ///
    /// Mọi giới hạn đều SUY RA TỪ KÍCH THƯỚC PHẦN ĐẤT của map (collider nền) chứ không phải số mềm:
    ///  - Bề ngang map = [<see cref="mapMinX"/>, <see cref="mapMaxX"/>] = MÉP TRÁI/PHẢI CỦA PHẦN ĐẤT
    ///    NHÌN THẤY (giao giữa collider nền và hình đang hiện). Vùng nhìn không bao giờ vượt ra ngoài
    ///    khoảng này, và còn dừng cách mỗi mép <see cref="mapEdgeInset"/> đơn vị (thụt vào trong một
    ///    chút). Khi đã zoom đủ xa để thấy gần hết bề ngang map thì camera CĂN GIỮA, 2 bên luôn cân nhau.
    ///  - Mức zoom xa nhất = vừa đủ thấy hết bề ngang map theo ĐÚNG tỉ lệ màn hình hiện tại (màn hình
    ///    càng hẹp thì phải zoom xa hơn mới thấy hết): mapWidth / (2 * aspect).
    ///  - Khung hình dọc: ở zoom mặc định mặt đất nằm ở <see cref="groundViewportFraction"/> chiều cao
    ///    màn hình; khi zoom ra thì ĐÁY khung hình bị chặn ở đáy lớp đất (<see cref="groundBottomY"/>)
    ///    nên không bao giờ lộ vùng trống bên dưới đất. Giới hạn zoom xa được chọn để ở mức xa nhất
    ///    camera đạt đúng <see cref="maxZoomOutCameraY"/> - vừa giữ được đáy, vừa không thừa quá nhiều trời.
    ///
    /// Số liệu lấy từ map hiện tại (Assets/Scenes/Gameplay.unity):
    ///  - Phần đất (Ground nâu + Grass xanh) rộng ~200 đơn vị, x ∈ [-99.6 .. 100.4]. Mép trên Grass -
    ///    mặt mà lính đứng lên (xem Slime.AlignToGround) - ở y = -0.70; đáy lớp đất nâu ở y = -7.66.
    ///  - Phe xanh: điểm ra lính/lâu đài ở x ≈ -77.8; King xanh ở x ≈ -65.6.
    ///  - Phe đỏ: điểm ra lính/lâu đài ở x ≈ 80.5; King đỏ ở x ≈ 69.8.
    ///  Cả 2 King đều nằm gọn trong phần đất nên chặn camera theo mép đất không cắt mất thành nào.
    ///
    /// Mặc định bật <see cref="autoDetectMapBounds"/> để tự dò lại các con số trên từ collider trong
    /// scene lúc Start - nới map xong không phải chỉnh tay. Tắt đi thì dùng số gõ trong Inspector.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Pan (kéo để dịch)")]
        [SerializeField] private float panSpeed = 15f;

        [Header("Biên map (theo mép phần đất nhìn thấy)")]
        [Tooltip("Tự dò mép trái/phải của PHẦN ĐẤT NHÌN THẤY (collider nền giao với hình đang hiện) và " +
                 "đáy đất, lúc Start. Camera sẽ không bao giờ vượt ra ngoài mép đất ở hai bên/dưới.")]
        [SerializeField] private bool autoDetectMapBounds = true;

        [Tooltip("Mép trái/phải phần đất nhìn thấy - chỉ dùng khi TẮT tự dò. Số mặc định lấy từ map " +
                 "hiện tại: -99.6 .. 100.4.")]
        [SerializeField] private float mapMinX = -99.6f;
        [SerializeField] private float mapMaxX = 100.4f;

        [Header("Zoom")]
        [Tooltip("Orthographic size lúc bắt đầu. Scene đang để 12.")]
        [SerializeField] private float defaultZoom = 12f;

        [Tooltip("Zoom gần nhất. Số càng nhỏ càng gần; 7 = nhìn được 14 đơn vị theo chiều cao.")]
        [SerializeField] private float minZoom = 7f;

        [Tooltip("Chặn cứng mức zoom xa nhất. Để 0 = tự tính 'vừa đủ thấy hết bề ngang map' theo " +
                 "tỉ lệ màn hình (màn hình hẹp/portrait thì trị số này lớn). Đặt > 0 nếu muốn giới hạn.")]
        [SerializeField] private float maxZoom = 0f;

        [Tooltip("Cho zoom xa thêm bao nhiêu phần so với mức vừa đủ thấy hết map. Để 0 = lúc zoom xa " +
                 "nhất thì bề ngang map vừa khít màn hình (2 mép map chạm đúng 2 mép màn hình).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float zoomOutMargin = 0f;

        [Tooltip("Một nấc lăn chuột đổi mức zoom bao nhiêu lần (1.2 = ±20% mỗi nấc).")]
        [SerializeField] private float wheelZoomPerNotch = 1.2f;

        [Tooltip("Độ nhạy chụm 2 ngón: hai ngón xa nhau bao nhiêu lần thì zoom ra bấy nhiêu lần.")]
        [SerializeField] private float pinchSensitivity = 1f;

        [Tooltip("Thời gian làm mượt zoom (giây). 0 = zoom tức thì.")]
        [SerializeField] private float zoomSmoothTime = 0.08f;

        [Header("Khung hình dọc")]
        [Tooltip("Y mặt đất - nơi chân lính đứng. Mép trên của Grass hiện tại là y = -0.70.")]
        [SerializeField] private float groundY = -0.7f;

        [Tooltip("Đáy khung hình KHÔNG được xuống dưới mức này - đáy lớp đất nâu (Ground). Nhờ vậy khi\n" +
                 "zoom xa, đất vẫn nằm ở đáy màn hình, không lộ vùng trống bên dưới đất.\n" +
                 "Đáy Ground hiện tại là y = -7.65. Bật tự dò biên thì giá trị này được tính lại.")]
        [SerializeField] private float groundBottomY = -7.65f;

        [Tooltip("Ở mức zoom mặc định, mặt đất nằm ở bao nhiêu phần chiều cao màn hình tính từ đáy.\n" +
                 "0.18 khớp với khung hình hiện tại (camera y = 6.98, zoom 12).")]
        [Range(0.02f, 0.5f)]
        [SerializeField] private float groundViewportFraction = 0.18f;

        [Tooltip("Y của camera ở mức zoom XA NHẤT. Vì đáy khung hình luôn bám đáy đất nên giá trị này\n" +
                 "đồng thời là GIỚI HẠN zoom out: số nhỏ hơn => zoom xa nhất ít hơn, bớt trời hơn\n" +
                 "(đổi lại thấy ít bề ngang map hơn). 39.586 là mức đang vừa đẹp.")]
        [SerializeField] private float maxZoomOutCameraY = 39.58604f;

        [Tooltip("Khung hình dừng cách MÉP TRÁI/PHẢI của map chừng này (world unit) - cho camera thụt\n" +
                 "vào trong một chút thay vì chạm đúng đường biên hai bên.\n" +
                 "Biên dưới KHÔNG bị ảnh hưởng: đáy khung hình vẫn bám đúng đáy lớp đất.\n" +
                 "0 = chạm đúng biên; 1 = thụt vào 1 đơn vị.")]
        [Min(0f)]
        [SerializeField] private float mapEdgeInset = 1f;

        [Header("Ảnh nền (backdrop)")]
        [Tooltip("SpriteRenderer làm nền trời - thường là object con của camera. Để trống sẽ tự tìm " +
                 "SpriteRenderer con của camera.")]
        [SerializeField] private SpriteRenderer backdrop;

        [Tooltip("Cho ảnh nền tự giãn để luôn phủ kín khung hình ở MỌI mức zoom. Vì nền là con của " +
                 "camera nên coi như ở vô cực: cây cối giữ nguyên cỡ trên màn hình và không bao giờ " +
                 "lộ vùng trống phía sau khi zoom out.")]
        [SerializeField] private bool backdropCoversView = true;

        private Camera cam;
        private float targetZoom;
        private float zoomVelocity;
        private Vector3 dragOrigin;
        private bool wasPinching;

        private Vector3 backdropBaseLocalScale;
        private Vector3 backdropBaseWorldSize;
        private bool backdropPrepared;
        private bool backdropWarningLogged;

        /// <summary>Bề ngang vùng chơi (world unit).</summary>
        private float MapWidth => Mathf.Max(1f, mapMaxX - mapMinX);

        /// <summary>Tỉ lệ màn hình; chưa có camera (lúc vẽ gizmo) thì tạm coi là 16:9.</summary>
        private float Aspect => (cam != null && cam.aspect > 0.01f) ? cam.aspect : (16f / 9f);

        /// <summary>
        /// Mức zoom xa nhất, lấy giá trị NHỎ NHẤT trong các giới hạn:
        ///  - vừa đủ thấy hết bề ngang map (đã trừ <see cref="mapEdgeInset"/> ở 2 bên) theo tỉ lệ màn
        ///    hình hiện tại, cộng lề,
        ///  - mức mà camera đạt <see cref="maxZoomOutCameraY"/> khi đáy khung hình bám đáy đất
        ///    (lúc đó camY = groundBottomY + size),
        ///  - <see cref="maxZoom"/> nếu có đặt (> 0).
        /// </summary>
        private float MaxAllowedZoom
        {
            get
            {
                float inset = Mathf.Max(0f, mapEdgeInset);

                float fitWholeMap = Mathf.Max(1f, MapWidth * (1f + zoomOutMargin) - inset * 2f) / (2f * Aspect);
                float byCameraY = maxZoomOutCameraY - groundBottomY;

                float limit = Mathf.Min(fitWholeMap, byCameraY);
                if (maxZoom > 0f) limit = Mathf.Min(limit, maxZoom);

                return Mathf.Max(minZoom, limit);
            }
        }

        private void Start()
        {
            cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("CameraController: không tìm thấy Camera.main (camera chưa gắn tag MainCamera?).");
                enabled = false;
                return;
            }

            DetectMapBounds();   // dò biên map TRƯỚC khi dùng để tính zoom/khung hình

            targetZoom = Mathf.Clamp(defaultZoom, minZoom, MaxAllowedZoom);
            cam.orthographicSize = targetZoom;

            PrepareBackdrop();   // phải đo nền gốc TRƯỚC khi ApplyFraming giãn nó
            ApplyFraming(0f);

            // In ra mức zoom xa nhất + vị trí Y camera ở mức đó để tiện đối chiếu bằng mắt.
            float maxZoomSize = MaxAllowedZoom;
            float maxZoomCameraY = FramedCameraY(maxZoomSize);
            Debug.Log($"CameraController: zoom xa nhất {maxZoomSize:F2} (aspect {Aspect:F3}) => " +
                      $"camera Y = {maxZoomCameraY:F3}, đáy khung hình y = {maxZoomCameraY - maxZoomSize:F3} " +
                      $"(đáy đất = {groundBottomY:F2})");
        }

        private void Update()
        {
            if (cam == null) return;

            HandleZoomInput();
            HandlePanInput();
            ApplyFraming(Time.deltaTime);
        }

        // ==========================================
        // ZOOM
        // ==========================================

        private void HandleZoomInput()
        {
            // --- Điện thoại: chụm 2 ngón ---
            if (Input.touchCount >= 2)
            {
                Touch first = Input.GetTouch(0);
                Touch second = Input.GetTouch(1);

                // Vị trí 2 ngón ở frame trước = vị trí hiện tại trừ delta của chính nó.
                Vector2 firstPrev = first.position - first.deltaPosition;
                Vector2 secondPrev = second.position - second.deltaPosition;

                float prevDistance = Vector2.Distance(firstPrev, secondPrev);
                float currentDistance = Vector2.Distance(first.position, second.position);

                if (prevDistance > 1f && currentDistance > 1f)
                {
                    // 2 ngón xa nhau ra => phóng to => orthographic size GIẢM.
                    float ratio = prevDistance / currentDistance;
                    targetZoom *= Mathf.Pow(ratio, pinchSensitivity);
                }

                wasPinching = true;
                return;
            }

            // Vừa nhấc ngón sau khi chụm: bỏ 1 nhịp để không nhảy zoom/pan.
            if (wasPinching)
            {
                wasPinching = false;
                return;
            }

            // --- Editor / desktop: lăn chuột ---
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) <= 0.0001f) return;

            // Axis này trả ~0.1 mỗi nấc nên chia 0.1 để ra số nấc. Lăn lên (scroll > 0) = zoom in.
            float notches = scroll / 0.1f;
            targetZoom *= Mathf.Pow(wheelZoomPerNotch, -notches);
        }

        // ==========================================
        // PAN
        // ==========================================

        private void HandlePanInput()
        {
            // 2 ngón dành cho zoom nên không pan, tránh giật.
            if (wasPinching || Input.touchCount >= 2) return;

            // --- Điện thoại: kéo 1 ngón. Quy đổi pixel -> world theo mức zoom hiện tại nên kéo
            //     ở mức zoom nào cũng bám đúng ngón tay.
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Moved)
                {
                    float worldPerPixel = (2f * cam.orthographicSize * Aspect) / Mathf.Max(1f, Screen.width);
                    PanBy(-touch.deltaPosition.x * worldPerPixel);
                }

                return;
            }

            // --- Editor / desktop: giữ chuột trái và kéo ---
            if (Input.GetMouseButtonDown(0))
            {
                dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
            }

            if (Input.GetMouseButton(0))
            {
                Vector3 difference = dragOrigin - cam.ScreenToWorldPoint(Input.mousePosition);
                PanBy(difference.x);
            }

            float horizontal = Input.GetAxis("Horizontal");
            if (Mathf.Abs(horizontal) > 0f)
            {
                PanBy(horizontal * panSpeed * Time.deltaTime);
            }
        }

        private void PanBy(float worldDeltaX)
        {
            if (Mathf.Approximately(worldDeltaX, 0f)) return;

            Vector3 position = transform.position;
            position.x = ClampX(position.x + worldDeltaX);
            transform.position = position;
        }

        // ==========================================
        // BIÊN & KHUNG HÌNH
        // ==========================================

        /// <summary>
        /// Chặn tâm camera theo X sao cho vùng nhìn không vượt ra ngoài biên map, VÀ còn thụt vào
        /// trong <see cref="mapEdgeInset"/> đơn vị ở MÉP TRÁI/PHẢI. Nếu vùng nhìn đã rộng hơn cả map
        /// (hết chỗ để pan) thì căn giữa.
        /// </summary>
        private float ClampX(float x)
        {
            float halfWidth = cam.orthographicSize * Aspect;
            float inset = Mathf.Max(0f, mapEdgeInset);

            float left = mapMinX + inset + halfWidth;
            float right = mapMaxX - inset - halfWidth;

            if (left >= right) return (mapMinX + mapMaxX) * 0.5f;

            return Mathf.Clamp(x, left, right);
        }

        /// <summary>
        /// Đặt lại kích thước & vị trí camera mỗi frame: làm mượt zoom rồi chặn X và tính lại Y.
        /// </summary>
        private void ApplyFraming(float deltaTime)
        {
            targetZoom = Mathf.Clamp(targetZoom, minZoom, MaxAllowedZoom);

            if (zoomSmoothTime > 0f && deltaTime > 0f)
            {
                cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
            }
            else
            {
                cam.orthographicSize = targetZoom;
            }

            FitBackdropToView();

            Vector3 position = transform.position;
            position.x = ClampX(position.x);
            position.y = FramedCameraY(cam.orthographicSize);
            transform.position = position;
        }

        /// <summary>
        /// Y của camera. Ở mức zoom mặc định thì giữ đúng khung hình hiện tại (mặt đất nằm ở
        /// <see cref="groundViewportFraction"/> chiều cao màn hình). Zoom càng xa thì càng bị
        /// <see cref="groundBottomY"/> kéo lên để ĐÁY khung hình luôn nằm trên đáy lớp đất: không bao
        /// giờ lộ vùng trống bên dưới đất, và dải đất không bị lơ lửng giữa màn hình.
        ///
        /// Giới hạn zoom xa (<see cref="MaxAllowedZoom"/>) được chọn sao cho ở mức zoom xa nhất camera
        /// đạt đúng <see cref="maxZoomOutCameraY"/> - hai điều kiện khớp nhau nên vừa giữ được đáy,
        /// vừa không thừa quá nhiều trời.
        /// </summary>
        private float FramedCameraY(float size)
        {
            float groundFramedY = groundY + size * (1f - 2f * groundViewportFraction);
            float lowestAllowedY = groundBottomY + size;   // đáy khung hình bám đáy lớp đất

            return Mathf.Max(groundFramedY, lowestAllowedY);
        }

        // ==========================================
        // DÒ BIÊN MAP TỪ SCENE
        // ==========================================

        /// <summary>
        /// Tính mép trái/phải của PHẦN ĐẤT NHÌN THẤY và đáy lớp đất từ chính hình học trong scene:
        /// gộp mọi collider TĨNH (nền Ground/Grass, tường...), nhưng mỗi cái chỉ lấy phần GIAO với
        /// hình đang hiện trên cùng object đó.
        ///
        /// Vì sao phải giao: collider và hình hiển thị có thể không khớp - ví dụ bạn kéo rộng collider
        /// của nền ra cho lính có chỗ đi, còn quad Brick/grass vẫn giữ nguyên bề ngang cũ. Khi đó "phần
        /// đất" mà mắt thấy chỉ bằng phần hình, nên biên map phải theo phần hình, không thì zoom xa
        /// nhất sẽ thấy camera thừa ra ngoài mép đất.
        ///
        /// Bỏ qua lính, King và đạn: biên map phải là MÉP ĐẤT, nhờ vậy camera không bao giờ nhìn thấy
        /// vùng ngoài phần đất ở hai bên.
        /// </summary>
        private void DetectMapBounds()
        {
            if (!autoDetectMapBounds) return;

            bool found = false;
            Bounds union = default(Bounds);

            foreach (Collider2D col in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            {
                if (col == null || !col.enabled) continue;
                if (col.isTrigger) continue;                                  // cổng/trigger không tính
                if (col.GetComponentInParent<Slime>() != null) continue;       // lính + King: không tính
                if (col.GetComponentInParent<Bullet>() != null) continue;      // đạn

                Bounds bounds = col.bounds;

                // Cắt theo phần hình đang hiện trên cùng object (nếu có), để biên map đúng bằng
                // phần đất VỪA NHÌN THẤY VỪA ĐI ĐƯỢC.
                Renderer view = col.GetComponent<Renderer>();
                if (view != null && view.enabled)
                {
                    Bounds visible = view.bounds;
                    if (visible.size.x > 0.001f && visible.size.y > 0.001f)
                    {
                        bounds.min = Vector3.Max(bounds.min, visible.min);
                        bounds.max = Vector3.Min(bounds.max, visible.max);

                        if (col.bounds.size.x - bounds.size.x > 0.5f)
                        {
                            Debug.LogWarning(
                                $"CameraController: collider của '{col.name}' rộng {col.bounds.size.x:F2} " +
                                $"nhưng phần hình đang hiện chỉ rộng {visible.size.x:F2} => đã cắt biên map " +
                                "theo phần hình. Muốn lính đi được hết bề ngang collider thì kéo giãn hình " +
                                "(transform scale của quad) cho khớp.");
                        }
                    }
                }

                if (bounds.size.x <= 0f || bounds.size.y <= 0f) continue;      // giao rỗng: bỏ

                if (found) union.Encapsulate(bounds);
                else
                {
                    union = bounds;
                    found = true;
                }
            }

            if (!found)
            {
                Debug.LogWarning("CameraController: không tìm thấy collider nào để dò biên map, " +
                                 "dùng số gõ trong Inspector.");
                return;
            }

            mapMinX = union.min.x;
            mapMaxX = union.max.x;
            groundBottomY = union.min.y;

            Debug.Log($"CameraController: dò biên map => x[{mapMinX:F2} .. {mapMaxX:F2}] " +
                      $"(rộng {mapMaxX - mapMinX:F2}), đáy đất y = {groundBottomY:F2}");
        }

        // ==========================================
        // ẢNH NỀN LUÔN PHỦ KÍN KHUNG HÌNH
        // ==========================================

        /// <summary>
        /// Ghi nhớ kích thước GỐC của ảnh nền một lần duy nhất (lúc chưa bị script can thiệp), để mỗi
        /// frame sau chỉ việc nhân lại theo khung hình hiện tại thay vì nhân dồn (nhân dồn sẽ trôi).
        /// </summary>
        private void PrepareBackdrop()
        {
            if (!backdropCoversView) return;

            if (backdrop == null) backdrop = FindBackdrop();

            if (backdrop == null || backdrop.sprite == null)
            {
                if (!backdropWarningLogged)
                {
                    backdropWarningLogged = true;
                    Debug.LogWarning("CameraController: không tìm thấy SpriteRenderer ảnh nền (object con " +
                                     "của camera) nên không giữ được nền phủ kín khung hình khi zoom.");
                }

                return;
            }

            backdropBaseLocalScale = backdrop.transform.localScale;
            backdropBaseWorldSize = backdrop.bounds.size;
            backdropPrepared = true;
        }

        /// <summary>
        /// Giãn ảnh nền theo kiểu "cover": đủ lớn để phủ kín khung hình mà vẫn giữ đúng tỉ lệ ảnh.
        ///
        /// Phải tính cả độ lệch của nền so với tâm camera: nền bị đẩy lên/xuống bao nhiêu thì phải
        /// cao thêm bấy nhiêu mới không hở mép. Nhờ vậy ở mức zoom mặc định nền gần như y nguyên như
        /// tác giả đặt, mà zoom ra bao xa cũng không lộ vùng trống.
        /// </summary>
        private void FitBackdropToView()
        {
            if (!backdropCoversView || !backdropPrepared || backdrop == null) return;
            if (backdropBaseWorldSize.x <= 0.001f || backdropBaseWorldSize.y <= 0.001f) return;

            Vector3 offset = backdrop.transform.localPosition;   // lệch so với tâm camera (camera không xoay)
            float halfWidth = cam.orthographicSize * Aspect;

            float needWidth = 2f * (halfWidth + Mathf.Abs(offset.x));
            float needHeight = 2f * (cam.orthographicSize + Mathf.Abs(offset.y));

            float factor = Mathf.Max(needWidth / backdropBaseWorldSize.x, needHeight / backdropBaseWorldSize.y);
            backdrop.transform.localScale = backdropBaseLocalScale * factor;
        }

        /// <summary>Ảnh nền là object con của camera (dính theo màn hình).</summary>
        private SpriteRenderer FindBackdrop()
        {
            return (cam != null) ? cam.GetComponentInChildren<SpriteRenderer>(true) : null;
        }

        /// <summary>
        /// Kẹp các giá trị cấu hình về khoảng dùng được, tránh chỉnh sai trong Inspector (ví dụ
        /// mapMaxX <= mapMinX hay minZoom = 0) làm camera zoom ra vô cực / mất khung hình.
        /// </summary>
        private void OnValidate()
        {
            minZoom = Mathf.Max(0.5f, minZoom);
            defaultZoom = Mathf.Max(minZoom, defaultZoom);
            maxZoom = Mathf.Max(0f, maxZoom);
            wheelZoomPerNotch = Mathf.Max(1.01f, wheelZoomPerNotch);
            pinchSensitivity = Mathf.Max(0.01f, pinchSensitivity);
            zoomSmoothTime = Mathf.Max(0f, zoomSmoothTime);
            mapEdgeInset = Mathf.Max(0f, mapEdgeInset);

            if (mapMaxX <= mapMinX) mapMaxX = mapMinX + 1f;
            if (groundBottomY > groundY) groundBottomY = groundY;

            // Zoom xa nhất phải lớn hơn minZoom, mà camY ở zoom xa nhất = groundBottomY + size.
            if (maxZoomOutCameraY <= groundBottomY + minZoom) maxZoomOutCameraY = groundBottomY + minZoom + 1f;
        }

        /// <summary>
        /// Vẽ biên map (cam) và giới hạn pan (xanh) khi chọn object này trong Editor - để chỉnh
        /// <see cref="mapMinX"/>/<see cref="mapMaxX"/> bằng mắt.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Camera editorCam = (cam != null) ? cam : Camera.main;
            float size = (editorCam != null) ? editorCam.orthographicSize : defaultZoom;
            float aspect = (editorCam != null && editorCam.aspect > 0.01f) ? editorCam.aspect : (16f / 9f);
            float halfWidth = size * aspect;

            float bottom = groundBottomY;
            float top = groundY + Mathf.Max(5f, groundY - groundBottomY);

            Gizmos.color = new Color(1f, 0.6f, 0.1f);   // biên map
            Gizmos.DrawLine(new Vector3(mapMinX, bottom, 0f), new Vector3(mapMinX, top, 0f));
            Gizmos.DrawLine(new Vector3(mapMaxX, bottom, 0f), new Vector3(mapMaxX, top, 0f));

            Gizmos.color = new Color(0.2f, 0.9f, 1f);   // giới hạn tâm camera khi pan
            Gizmos.DrawLine(new Vector3(mapMinX + halfWidth, bottom, 0f), new Vector3(mapMinX + halfWidth, top, 0f));
            Gizmos.DrawLine(new Vector3(mapMaxX - halfWidth, bottom, 0f), new Vector3(mapMaxX - halfWidth, top, 0f));

            Gizmos.color = Color.yellow;                 // mặt đất (chân lính) và đáy lớp đất
            Gizmos.DrawLine(new Vector3(mapMinX, groundY, 0f), new Vector3(mapMaxX, groundY, 0f));
            Gizmos.DrawLine(new Vector3(mapMinX, groundBottomY, 0f), new Vector3(mapMaxX, groundBottomY, 0f));
        }
    }
}
