using System;
using UnityEngine;

/// <summary>
/// Cấu hình HỆ THỐNG VFX của trận đấu: các prefab hiệu ứng (Cartoon FX Remaster) cho những sự kiện dùng
/// hiệu ứng dựng sẵn, cộng thêm phần thông số cho 2 hiệu ứng VẼ BẰNG CODE (thiên thạch rơi, aura buff) -
/// xem <see cref="VfxManager"/> để biết chỗ phát.
///
/// Asset này nằm ở <c>Assets/Resources/VfxConfig.asset</c> nên <see cref="VfxManager"/> tự nạp lúc
/// chạy (Resources.Load) - KHÔNG cần gắn gì vào scene, cả chơi đơn lẫn MultiPlayer đều có hiệu ứng.
/// Muốn đổi prefab / art / màu thì chọn asset này trong Project và sửa ở Inspector.
/// </summary>
[CreateAssetMenu(fileName = "VfxConfig", menuName = "Fortress Clash/VFX Config")]
public class VfxConfig : ScriptableObject
{
    [Tooltip("CFXR Flash - hiệu ứng khi một lính VỪA ĐƯỢC SINH RA (mọi loại lính, cả 2 phe).")]
    public GameObject spawnFlash;

    [Tooltip("CFXR Magic Poof - hiệu ứng khi NGƯỜI CHƠI THẮNG, phát ở giữa màn hình lúc hiện popup kết quả.")]
    public GameObject winPoof;

    [Tooltip("CFXR3 Hit Misc F Smoke - hiệu ứng khi một lính BỊ HẠ GỤC (mọi loại lính, cả 2 phe; không tính King).")]
    public GameObject deathSmoke;

    [Tooltip("CFXR3 Hit Leaves A (Lit) - hiệu ứng khi NGƯỜI CHƠI THUA, phát ở giữa màn hình lúc hiện popup kết quả.")]
    public GameObject loseLeaves;

    [Tooltip("Hiệu ứng KỸ NĂNG THIÊN THẠCH rơi - phát tại điểm rơi trên sân (xem AbilityBar). " +
             "Để trống thì kỹ năng vẫn gây sát thương bình thường, chỉ là không có hiệu ứng. " +
             "Hiện đang để trống: đợt thiên thạch rơi dùng hiệu ứng ở ô Meteor bên dưới.")]
    public GameObject abilityImpact;

    [Tooltip("Hiệu ứng ĐỢT THIÊN THẠCH RƠI của kỹ năng Thiên thạch (nút btn_meteor) - xem MeteorStrike. " +
             "Để TRỐNG ô Sprite thì kỹ năng vẫn gây sát thương ngay như trước, chỉ là không có hiệu ứng.")]
    public MeteorStrikeSettings meteor = new MeteorStrikeSettings();

    /// <summary>
    /// Thông số hiệu ứng ĐỢT THIÊN THẠCH RƠI (kỹ năng Thiên thạch - xem <see cref="MeteorStrike"/>).
    ///
    /// Art là ảnh <c>Assets/Textures/meteor.png</c>: viên đá có SẴN đuôi lửa chếch lên phải, nên ở đây
    /// không vẽ thêm vệt lửa mà chỉ LẬT ảnh (mirrorSprite) + xoay ảnh cho đuôi hướng về phía nó bay tới,
    /// và bù chỗ lệch giữa ĐẦU ĐÁ với tâm ảnh. Hai ô <c>tailAngleDegrees</c> / <c>headOffset</c> là số đo
    /// của chính ảnh đó - đổi art khác thì phải đo lại 2 số này.
    ///
    /// Hướng rơi: phe XANH (người chơi) đứng bên TRÁI map nên mặc định thiên thạch bay từ trên cao bên
    /// TRÁI chéo xuống phải - tức từ phía bên gọi ra. Đổi thành từ phía ĐỎ thì tắt <c>comesFromBlueSide</c>.
    ///
    /// SÁT THƯƠNG: mỗi viên chạm đất gây sát thương một lần (xem <see cref="GameManager.ApplyAreaDamage"/>),
    /// nên một lính nằm giữa vùng nổ có thể ăn nhiều viên - tối đa bằng <c>count</c> lần. Muốn chỉnh độ
    /// mạnh thì sửa ô Damage của kỹ năng ở component AbilityBar trong scene.
    /// </summary>
    [Serializable]
    public class MeteorStrikeSettings
    {
        [Tooltip("Ảnh viên thiên thạch (Assets/Textures/meteor.png).")]
        public Sprite sprite;

        [Tooltip("Cỡ ảnh viên thiên thạch (cả đuôi lửa) theo world unit. Đầu đá chiếm khoảng 1/3 bề ngang ảnh.")]
        [Min(0.2f)] public float spriteWidth = 9f;

        [Tooltip("Số viên rơi mỗi lần bấm. Kỹ năng có bán kính lớn (AbilityBar.radius) nên cần đủ viên để " +
                 "trải khắp vùng nổ chứ không dồn một chỗ.")]
        [Min(1)] public int count = 8;

        [Tooltip("Số viên (kể cả viên đầu) dồn SÁT mục tiêu - tức con lính tuyến đầu của phe địch " +
                 "(xem AbilityBar.CastMeteor). Số còn lại rải rộng ra cho vùng nổ. " +
                 "Đặt bằng count = cả đợt dồn hết vào mục tiêu.")]
        [Min(1)] public int concentratedCount = 3;

        [Tooltip("Bán kính rải của nhóm DỒN SÁT mục tiêu, tính theo phần bán kính kỹ năng. " +
                 "Nhỏ = dồn sát mục tiêu (nhìn rõ cả đợt đập vào một chỗ), lớn = rải đều cả vùng nổ.")]
        [Range(0f, 1f)] public float concentratedSpread = 0.18f;

        [Tooltip("BẬT = thiên thạch bay từ phía XANH (bên người chơi, bên trái map) chéo xuống phải, tức từ " +
                 "phía bên gọi ra. TẮT = từ phía ĐỎ. (Map hiện tại: xanh bên trái, đỏ bên phải.)")]
        public bool comesFromBlueSide = true;

        [Tooltip("Lật ảnh theo chiều ngang. Art meteor.png vẽ đuôi lửa chếch lên PHẢI nên khi bay về phía " +
                 "phải phải lật lại cho đuôi hướng lên TRÁI; nhờ lật mà viên đá vẫn nằm gần như thẳng đứng " +
                 "thay vì bị xoay gần 80 độ.")]
        public bool mirrorSprite = true;

        [Tooltip("Góc rơi: số độ chếch xuống so với phương ngang. 90 = rơi thẳng đứng, 58 = chếch rõ.")]
        [Range(15f, 90f)] public float fallAngleDegrees = 58f;

        [Tooltip("Hướng đuôi lửa TRONG ẢNH GỐC, tính bằng số độ chếch lên so với phương ngang " +
                 "(ảnh meteor.png hiện tại: ~43). Hiệu ứng dùng số này để xoay viên đá cho đuôi luôn " +
                 "hướng về phía nó bay tới.")]
        [Range(0f, 90f)] public float tailAngleDegrees = 43f;

        [Tooltip("Vị trí ĐẦU ĐÁ trong ảnh, so với tâm ảnh (x sang phải, y lên; đơn vị = bề ngang/bề cao ảnh). " +
                 "Dùng để bù cho việc đầu đá lệch khỏi tâm ảnh, nhờ vậy ĐẦU ĐÁ chạm đúng điểm nổ.")]
        public Vector2 headOffset = new Vector2(-0.29f, 0.12f);

        [Tooltip("Thời gian một viên rơi từ ngoài màn hình xuống đất (giây). Càng lớn càng chậm và càng dễ " +
                 "nhìn. Đây cũng là khoảng thời gian từ lúc bấm nút tới lúc viên đầu tiên gây sát thương.")]
        [Min(0.05f)] public float fallSeconds = 1.7f;

        [Tooltip("Lệch nhịp giữa các viên (giây) để chúng rơi nối đuôi nhau thay vì cùng lúc.")]
        [Min(0f)] public float staggerSeconds = 0.09f;

        [Tooltip("Bán kính rải của nhóm viên CÒN LẠI, tính theo phần bán kính kỹ năng. " +
                 "1 = rải tới đúng mép vùng nổ; lớn hơn 1 = vài viên rơi quá mép (rơi ngoài vùng sát thương).")]
        [Range(0f, 2f)] public float spreadScale = 1f;

        [Tooltip("Cỡ vụ nổ (ánh sáng loé + vòng sóng) so với bán kính kỹ năng. " +
                 "Bán kính kỹ năng khá lớn nên số này phải NHỎ, không thì vụ nổ phủ kín cả màn hình.")]
        [Min(0.05f)] public float impactScale = 0.14f;

        [Tooltip("Thứ tự vẽ: lớn hơn lính để thiên thạch bay TRƯỚC mặt lính (lính thường ở 0-4).")]
        public int sortingOrder = 50;

        [Tooltip("Màu ánh sáng loé và vòng sóng lúc chạm đất.")]
        public Color impactColor = new Color(1f, 0.72f, 0.3f, 0.9f);
    }
}
