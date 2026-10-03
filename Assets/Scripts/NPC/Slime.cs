using DG.Tweening;
using UnityEngine;

public class Slime : MonoBehaviour
{
    [Header("Base Stats")]
    public float velocity = 3f;
    public bool blueSide = true;
    public Vector3 Destination;
    public Vector3 SpawnPosition;
    public float attackRange = 1.5f;

    [Tooltip("Coi như đã tới đích khi còn cách Destination chừng này (world unit) thì dừng hẳn. " +
             "KHÔNG dùng attackRange làm mốc dừng ở đây: lính tầm xa có attackRange rất lớn nên " +
             "sẽ khựng lại giữa đường và không bao giờ tiến tới chỗ có địch để bắn.")]
    public float arriveDistance = 1.5f;

    [Tooltip("Quãng lao tới tối đa ngay khi vừa spawn (world unit). Sẽ TỰ GIẢM nếu phía trước đã có " +
             "đồng minh, để lính mới đứng vào CUỐI HÀNG chứ không đáp trúng lưng đồng đội. Nếu hàng " +
             "đồng minh đã lấn tới/qua điểm spawn thì lính mới LÙI RA SAU hàng (quãng lùi không bị " +
             "giới hạn bởi số này).")]
    public float spawnDashDistance = 4f;

    public int health = 45;
    public int damage = 10;
    public int price = 10;
    public int priceDrop = 5;

    [Header("Jump Settings (DOTween)")]
    public float jumpPower = 0.5f;
    public float jumpDuration = 0.35f;
    public float jumpDelay = 0.05f;
    public int numJumps = 1;

    [Header("Idle Animation - lúc đứng yên chờ đánh (DOTween)")]
    [Tooltip("Bật hiệu ứng 'thở' (co giãn nhẹ theo DOTween) khi lính đứng yên chờ: đứng chờ đánh, " +
             "chờ hàng tiến lên... Trước đây lính đứng im hoàn toàn nên nhìn như tượng.")]
    public bool enableIdleAnimation = true;

    [Tooltip("Biên độ nhịp thở: 0.08 = lúc hóp bụng thấp hơn 8% và bè ngang ra 8%.")]
    public float idleSquashAmount = 0.08f;

    [Tooltip("Thời gian MỘT nhịp thở (giây). Mỗi con lệch ngẫu nhiên ±15% để cả hàng không thở " +
             "y hệt nhau như máy.")]
    public float idleBreathDuration = 0.9f;

    [Tooltip("Góc RƯỚN NGƯỜI về phía trước khi đứng chờ (độ). Lính chờ đánh nghiêng hẳn về phía " +
             "địch thay vì đứng im như tượng. 0 = không nghiêng.")]
    public float idleLeanAngle = 5f;

    [Header("Ép sát khi còn ít chỗ (không nhảy)")]
    [Tooltip("Khi chỗ trống phía trước ngắn hơn mức này (world unit) thì lính ÉP SÁT dần (trượt tới) " +
             "thay vì nhảy một cú ngắn: nhờ vậy hàng đợi tiến vào ngay khi hàng trước vừa trống, và " +
             "lính dí sát mục tiêu hơn (không đứng hụt lại một quãng).")]
    public float pressForwardDistance = 1f;

    [Tooltip("Tốc độ ép sát (world unit / giây).")]
    public float pressForwardSpeed = 6f;

    [Header("Ally Blocking")]
    [Tooltip("Khoảng quét đồng minh phía trước (world unit) - chỉ là mức TỐI THIỂU, khoảng cách " +
             "thực sẽ tự lớn hơn nửa bề rộng body + allyQueueGap để lính không đè lên nhau.")]
    public float allyCheckDistance = 0.7f;
    public float allyCheckRadius = 0.25f;

    [Tooltip("Khe hở mép-với-mép giữa 2 lính cùng phe khi xếp hàng chờ đánh (world unit). " +
             "Đây là con số quyết định hàng đợi trông thoáng hay chen chúc: thân lính rất to " +
             "(BaseSlime ~5.25 world unit) nên để quá nhỏ thì sprite dính sát nhau. " +
             "Chỉnh ở đây nếu muốn hàng đợi rộng/hẹp hơn.")]
    public float allyQueueGap = 1f;

    [Header("Enemy Blocking (2 phe không đè lên nhau)")]
    [Tooltip("Khoảng hở TỐI THIỂU giữa 2 COLLIDER của 2 đơn vị KHÁC PHE (world unit). " +
             "Lính tiến tới rồi đứng lại ở khoảng hở này để đánh nhau nên không chồng lên nhau.")]
    public float enemyMinGap = 0.5f;

    [Header("Ground")]
    [Tooltip("Nâng/hạ lính so với mặt collider nền (Grass/Ground). Dương = nâng lên, âm = lún xuống.")]
    public float groundYOffset = 0f;

    public string characterName = "BaseSlime";

    [Header("Damage Flash (nhấp nháy đỏ khi trúng đòn)")]
    [Tooltip("Màu sprite lúc nhấp nháy. Màu này NHÂN lên sprite gốc: đậm (đỏ tươi) thì cả con " +
             "đỏ rực, hơi nhạt thì vẫn thấy hình bên dưới.")]
    public Color damageFlashColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Tooltip("Thời gian của MỘT nhịp nhấp nháy (giây).")]
    public float damageFlashDuration = 0.07f;

    [Tooltip("Số nhịp nhấp nháy cho mỗi lần trúng đòn (1 = nháy 1 cái, 2 = nháy 2 cái).")]
    public int damageFlashBlinks = 2;

    protected Tween moveTween;
    protected Tween idleTween;          // hiệu ứng "thở" lúc đứng yên chờ (xem UpdateIdleAnimation)
    protected bool isMoving = false;
    protected Vector3 originalScale;
    protected Quaternion originalRotation;

    protected GameObject target;

    protected virtual void Awake()
    {
        originalScale = transform.localScale;
        originalRotation = transform.localRotation;   // anim "rướn người" lúc chờ phải trả về đúng gốc
        groundY = transform.position.y;   // mặc định: đứng ngay tại chỗ được spawn
        CacheRestBodyWidth();
    }

    // ==========================================
    // KÍCH THƯỚC - XẾP HÀNG - TẠM DỪNG
    // ==========================================

    protected float groundY;              // Y mặt nền mà lính đứng lên
    protected bool isSpawning = false;    // đang chạy hiệu ứng pop lúc mới spawn

    private Collider2D bodyCollider;
    private float restHalfWidth = 0f;

    /// <summary>
    /// Đo bề rộng thân MỘT LẦN lúc Awake - lúc này chưa chạy hiệu ứng co giãn của cú nhảy/spawn.
    /// Mọi phép đo khoảng cách đều dùng số này, vì nếu dùng bounds hiện tại thì bề rộng collider
    /// phồng lên xẹp xuống theo animation (0.85x ... 1.3x) làm số đo nhảy loạn gây cà giật.
    /// </summary>
    private void CacheRestBodyWidth()
    {
        Collider2D own = GetComponent<Collider2D>();
        restHalfWidth = (own != null) ? own.bounds.size.x * 0.5f : 1f;
    }

    // Nhấp nháy đỏ khi trúng đòn (cache lại màu gốc để trả về nguyên trạng)
    private SpriteRenderer[] damageFlashRenderers;
    private Color[] damageFlashBaseColors;
    private Tween damageFlashTween;

    /// <summary>Collider thân của unit (để đo khoảng cách thật giữa 2 con khi xếp hàng).</summary>
    protected Collider2D BodyCollider
    {
        get
        {
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            return bodyCollider;
        }
    }

    /// <summary>Nửa bề rộng thân LÚC NGHỈ (không tính hiệu ứng co giãn) - dùng cho mọi phép đo.</summary>
    protected float RestHalfWidth
    {
        get
        {
            // Trong Editor (vẽ gizmo) Awake chưa chạy nên đo tại chỗ; lúc chơi thì Awake đã cache sẵn.
            if (restHalfWidth <= 0f) CacheRestBodyWidth();
            return restHalfWidth;
        }
    }

    /// <summary>
    /// Tìm mặt trên của collider nền (Grass/Ground) ngay dưới lính rồi đặt lính đứng lên đó:
    /// mép dưới collider của lính trùng mặt nền (cộng groundYOffset). groundY được cache lại
    /// để mọi cú nhảy sau đó đều đáp đúng mặt đất, không bị treo trên không.
    /// </summary>
    protected virtual void AlignToGround()
    {
        Collider2D body = GetComponent<Collider2D>();
        float surfaceY = FindGroundSurfaceY();

        if (body == null || float.IsNaN(surfaceY))
        {
            groundY = transform.position.y;
            return;
        }

        // Mép dưới collider của lính phải nằm đúng mặt nền (+offset)
        float delta = (surfaceY + groundYOffset) - body.bounds.min.y;
        transform.position = new Vector3(transform.position.x, transform.position.y + delta, transform.position.z);

        groundY = transform.position.y;
    }

    /// <summary>Mặt trên của collider nền cao nhất nằm dưới lính (bỏ qua lính khác và đạn).</summary>
    protected float FindGroundSurfaceY()
    {
        const float probeUp = 30f;
        Vector2 origin = new Vector2(transform.position.x, transform.position.y + probeUp);

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, probeUp * 2f);

        float surfaceY = float.NaN;
        float highest = float.NegativeInfinity;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.gameObject == gameObject) continue;
            if (hit.collider.GetComponentInParent<Slime>() != null) continue;    // bỏ qua lính/King khác
            if (hit.collider.GetComponentInParent<Bullet>() != null) continue;   // bỏ qua đạn

            float top = hit.collider.bounds.max.y;
            if (top > highest)
            {
                highest = top;
                surfaceY = top;
            }
        }

        return surfaceY;
    }

    /// <summary>Đưa lính về đúng mặt đất đã cache (gọi ở cuối mỗi cú nhảy).</summary>
    protected virtual void SnapToGround()
    {
        transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
    }

    /// <summary>
    /// Tầm quét đồng minh phía trước, tính từ TÂM mình (dùng cho CircleCastAll có bán kính
    /// <see cref="allyCheckRadius"/>).
    ///
    /// Vì tia quét có bán kính nên tầm tới THẬT = giá trị này + bán kính, nên ở đây TRỪ bán kính ra
    /// để tia vừa tới đúng chỗ "cách mép trước của mình allyQueueGap". Nhờ vậy lính hết bị coi là
    /// chắn NGAY khi hàng trước vừa đủ chỗ, không phải chờ thêm một quãng nữa mới nhích vào
    /// (trước đây còn phải chờ dư ra đúng bằng allyCheckRadius). Giá trị trong Inspector là mức
    /// TỐI THIỂU.
    /// </summary>
    protected float GetAllyCheckDistance(float inspectorDistance)
    {
        return Mathf.Max(inspectorDistance, RestHalfWidth + allyQueueGap - allyCheckRadius);
    }

    /// <summary>
    /// Dừng vì bị đồng minh chắn phía trước: KHÔNG Pause tween giữa chừng (sẽ làm lính treo
    /// lơ lửng trên không trung) mà để cú nhảy đang dở đáp xuống đất rồi mới đứng yên.
    /// </summary>
    protected virtual void StopMovingForAlly()
    {
        if (isSpawning) return;   // đang chạy hiệu ứng spawn thì đừng đụng vào
        isMoving = false;
    }

    /// <summary>Tiếp tục di chuyển nếu đang rảnh (bắt đầu cú nhảy mới từ vị trí hiện tại).</summary>
    protected virtual void ContinueMovingIfIdle()
    {
        if (isSpawning || isMoving) return;
        if (IsMoveTweenRunning) return;            // cú nhảy cũ còn đang bay => để nó đáp đã
        if (Destination == Vector3.zero) return;   // không có điểm đến (ví dụ King) => đứng yên
        StartMoving();
    }

    // ==========================================
    // NÂNG CẤP THÀNH
    // ==========================================

    /// <summary>
    /// Unit này có nhận chỉ số cộng thêm từ nâng cấp thành không. Mặc định mọi lính đều nhận,
    /// riêng King (thành) chỉ nhận khi bật <see cref="CastleUpgrade.applyToKing"/>.
    /// </summary>
    protected virtual bool ReceivesCastleUpgrade
    {
        get { return true; }
    }

    /// <summary>
    /// Cộng MÁU và SÁT THƯƠNG từ nâng cấp thành cho lính MỚI SPAWN của phe xanh.
    ///
    /// PHẢI gọi ở CUỐI Start() của class con: các class con (BaseSlime, Catapult...) gán lại
    /// health/damage của prefab trong Start() nên nếu cộng sớm hơn thì giá trị sẽ bị ghi đè.
    /// Lính đã ra trận trước đó không bị ảnh hưởng (chỉ lính spawn sau mới được cộng).
    /// </summary>
    protected virtual void ApplyCastleUpgrade()
    {
        if (!blueSide) return;             // chỉ phe người chơi (xanh) được cộng
        if (!ReceivesCastleUpgrade) return;

        if (GameManager.Instance == null) return;

        CastleUpgrade castle = GameManager.Instance.Castle;
        if (castle == null || castle.CurrentLevel <= 0) return;

        health = Mathf.RoundToInt(health * castle.HealthMultiplier);
        damage = Mathf.RoundToInt(damage * castle.DamageMultiplier);
    }

    protected virtual void Start()
    {
        if (blueSide && Destination == Vector3.zero)
            Destination = transform.position + Vector3.right * 20f;
        else if (!blueSide && Destination == Vector3.zero)
            Destination = transform.position + Vector3.left * 20f;

        AlignToGround();       // đứng lên mặt collider nền trước khi chạy hiệu ứng spawn
        PlaySpawnPopEffect();
    }

    /// <summary>
    /// Trượt tới sát một quãng ngắn, KHÔNG nhảy - dùng khi chỗ trống phía trước ít
    /// (&lt; <see cref="pressForwardDistance"/>). Mỗi frame chỉ đi một quãng nhỏ
    /// (<see cref="pressForwardSpeed"/> world unit/giây) nên nhìn như lính đang rướn tới chứ không
    /// giật cục. Bản thân hàm này không đụng tới <see cref="isMoving"/>; người gọi
    /// (<see cref="StartMoving"/>) phải tự hạ cờ đó xuống false sau khi gọi, để Update gọi lại được
    /// mỗi frame cho tới khi hết chỗ.
    ///
    /// Đây là chỗ sửa cảm giác "cận chiến không lại gần được": trước đây hết chỗ nhảy là lính đứng
    /// lại luôn, nên nó dừng hụt trước mục tiêu và trước hàng đợi một quãng nhìn thấy được.
    /// </summary>
    protected virtual void PressForward(float stepDistance, float direction)
    {
        if (stepDistance <= 0f) return;   // hết chỗ thật => đứng yên

        float creep = Mathf.Min(stepDistance, Mathf.Max(0.01f, pressForwardSpeed) * Time.deltaTime);
        if (creep <= 0f) return;

        Vector3 position = transform.position;
        transform.position = new Vector3(position.x + (creep * direction), position.y, position.z);
    }

    protected virtual void Update()
    {
        if (CheckAllyBlocking())
        {
            StopMovingForAlly();
            return;
        }

        ContinueMovingIfIdle();
    }

    protected virtual void StartMoving()
    {
        float distanceToDest = Mathf.Abs(transform.position.x - Destination.x);
        if (distanceToDest <= arriveDistance)
        {
            isMoving = false;   // đã tới đích => không còn "đang đi" nữa
            return;
        }

        float direction = blueSide ? 1f : -1f;

        // Cú nhảy không được vượt quá chỗ trống phía trước, nếu không lính sẽ nhảy đè lên
        // đồng minh đứng trước (xếp hàng bị dính vào nhau) hoặc nhảy lố qua mục tiêu.
        float stepDistance = Mathf.Min(4.5f, distanceToDest);
        stepDistance = Mathf.Min(stepDistance, GetClearDistanceAhead());

        // Còn ít chỗ thì ÉP SÁT dần (trượt tới) thay vì nhảy một cú ngắn: nhờ vậy hàng đợi tiến vào
        // ngay khi hàng trước vừa trống, và lính dí sát mục tiêu tới đúng khoảng cách đánh chứ
        // không đứng hụt lại một quãng (cú nhảy ngắn vừa xấu vừa để lại khe hở).
        if (stepDistance <= pressForwardDistance)
        {
            PressForward(stepDistance, direction);

            // PHẢI hạ cờ isMoving ở đây. Nhánh này KHÔNG tạo cú nhảy nào nên không có tween nào hạ
            // cờ giúp, mà cờ thì đang là true do cú nhảy trước để lại (xem OnComplete của StartMoving).
            // Để nguyên true thì ContinueMovingIfIdle() chặn luôn MỌI frame sau đó: lính đứng chết trân
            // khi còn cách mục tiêu ~1-2.5 đơn vị - NGOÀI tầm đánh - nên 2 phe gặp nhau mà không đánh
            // nhau. Hạ về false thì Update gọi lại mỗi frame, lính vẫn ép sát tới khi hết chỗ.
            isMoving = false;
            return;
        }

        isMoving = true;
        // Luôn bắt đầu cú nhảy từ mặt nền (không phải từ Y đang lơ lửng) để đáp xuống đúng mặt đất.
        Vector3 startPos = new Vector3(transform.position.x, groundY, transform.position.z);
        Vector3 targetPos = new Vector3(startPos.x + (stepDistance * direction), startPos.y, startPos.z);

        if (blueSide && targetPos.x > Destination.x) targetPos.x = Destination.x;
        if (!blueSide && targetPos.x < Destination.x) targetPos.x = Destination.x;

        Sequence slimeJumpSeq = DOTween.Sequence();

        slimeJumpSeq.Append(transform.DOScale(new Vector3(originalScale.x * 0.85f, originalScale.y * 0.85f, originalScale.z), jumpDuration * 0.2f).SetEase(Ease.OutQuad));

        slimeJumpSeq.Append(transform.DOMoveX(targetPos.x, jumpDuration).SetEase(Ease.Linear));
        slimeJumpSeq.Join(transform.DOScale(new Vector3(originalScale.x * 0.9f, originalScale.y * 1.25f, originalScale.z), jumpDuration * 0.5f).SetEase(Ease.OutQuad));

        Sequence jumpYSeq = DOTween.Sequence();
        jumpYSeq.Append(transform.DOMoveY(startPos.y + jumpPower, jumpDuration * 0.5f).SetEase(Ease.OutQuad));
        jumpYSeq.Append(transform.DOMoveY(targetPos.y, jumpDuration * 0.5f).SetEase(Ease.InQuad));

        slimeJumpSeq.Join(jumpYSeq);

        slimeJumpSeq.Append(transform.DOScale(new Vector3(originalScale.x * 1.3f, originalScale.y * 0.7f, originalScale.z), 0.1f));

        slimeJumpSeq.Append(transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack));

        slimeJumpSeq.OnComplete(() => {
            SnapToGround();   // đảm bảo luôn đứng trên mặt nền sau mỗi cú nhảy

            float currentDistToDest = Mathf.Abs(transform.position.x - Destination.x);
            if (currentDistToDest > arriveDistance && isMoving)
            {
                DOVirtual.DelayedCall(jumpDelay, () => {
                    if (isMoving) StartMoving();
                });
            }
            else
            {
                isMoving = false;
            }
        });

        moveTween = slimeJumpSeq;
    }

    private bool CheckAllyBlocking()
    {
        Vector2 rayDir = blueSide ? Vector2.right : Vector2.left;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, allyCheckRadius, rayDir, GetAllyCheckDistance(allyCheckDistance));

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingAlly(hit.collider)) return true;
        }
        return false;
    }

    /// <summary>
    /// Đơn vị này đánh xa không (bắn đạn / nã đá). Lớp con tầm xa override thành true.
    /// Dùng ở <see cref="SeparateFromAllies"/>: lính tầm xa tự lùi ra khi có lính cận chiến cùng phe
    /// đứng chồng hẳn lên nó.
    /// </summary>
    protected virtual bool IsRangedUnit
    {
        get { return false; }
    }

    /// <summary>
    /// Collider này có phải là đồng minh đang chắn đường không? King CÙNG PHE luôn bị bỏ qua:
    /// King đứng cố định ngay sát điểm spawn (collider của King thò ra trước mặt lính vừa spawn)
    /// nên nếu tính King là vật cản thì lính mới spawn sẽ bị kẹt tại chỗ, không đi được.
    /// King của ĐỐI PHƯƠNG vẫn là vật cản để lính dừng lại đánh.
    /// </summary>
    protected bool IsBlockingAlly(Collider2D collider)
    {
        if (collider == null || collider.gameObject == gameObject) return false;

        return AllyBlocksMe(collider.GetComponentInParent<Slime>());
    }

    /// <summary>
    /// Đồng minh này có chắn đường mình không? Dùng thống nhất cho cả 3 việc: kiểm tra "bị đồng minh
    /// chắn", giới hạn cú nhảy và chốt giãn cách cuối frame.
    ///
    /// Mọi lính cùng phe đều chắn đường nhau (không ai đi xuyên qua ai - hàng đợi đánh lần lượt),
    /// chỉ bỏ qua King cùng phe vì King đứng cố định ngay sát điểm spawn, collider thò ra trước mặt
    /// lính mới nên nếu tính là vật cản thì lính mới spawn bị kẹt tại chỗ.
    /// </summary>
    protected bool AllyBlocksMe(Slime other)
    {
        if (other == null || other == this) return false;
        if (other.blueSide != blueSide) return false;   // địch có luật riêng
        if (other is KingSlime) return false;           // King cùng phe không chắn đường

        return true;
    }

    /// <summary>
    /// Quãng dịch chuyển theo HƯỚNG TIẾN để lính mới đứng vào đúng CUỐI HÀNG đồng minh cùng phe:
    /// dương = tiến lên, ÂM = phải LÙI RA SAU hàng (hàng đã lấn tới/qua điểm spawn).
    ///
    /// MỌI đồng minh cùng phe đều tính là vật cản, kể cả con đang CHỒNG LÊN mình hay đứng SAU mình:
    /// mép thân mình phải cách mép thân nó ít nhất <see cref="allyQueueGap"/>. Vì lấy giá trị NHỎ NHẤT
    /// trên tất cả đồng minh nên kết quả chính là chỗ đứng sau con CUỐI HÀNG.
    ///
    /// Trước đây hàm bỏ qua đồng minh có khe hở âm (đang chồng lên nhau) và cắt kết quả về 0, nên khi
    /// hàng đã lấn tới điểm spawn thì lính mới KHÔNG "thấy" con đứng cuối hàng: nó cứ lao tới hết
    /// quãng và đáp trúng lưng con đó (chèn vào giữa hàng). Giờ khe hở âm cho kết quả âm => lính mới
    /// lùi ra sau hàng thay vì chèn vào giữa.
    ///
    /// KHÁC <see cref="GetClearDistanceAhead"/>: hàm này KHÔNG dùng physics mà duyệt thẳng danh sách
    /// <see cref="Slime"/> và đo bằng tâm + nửa bề rộng LÚC NGHỈ. Lý do: lính vừa được Instantiate thì
    /// collider chưa chắc đã được đồng bộ vào thế giới vật lý (project đặt
    /// Physics2D.autoSyncTransforms = 0), nên CircleCast có thể KHÔNG thấy đồng minh đang đứng ngay
    /// trước mặt -> lính mới đáp thẳng vào nó. Dùng cho cú lao tới lúc mới spawn, nơi việc đáp trúng
    /// đồng minh là không thể chấp nhận.
    /// </summary>
    protected float GetQueueSlotOffset(float maxDistance)
    {
        Collider2D own = BodyCollider;
        if (own == null) return maxDistance;

        float direction = blueSide ? 1f : -1f;
        float ownCenter = own.bounds.center.x;
        float offset = maxDistance;

        foreach (Slime other in FindObjectsByType<Slime>(FindObjectsSortMode.None))
        {
            if (other == null || other == this) continue;
            if (other.blueSide != blueSide) continue;
            if (other is KingSlime) continue;   // King cùng phe không chắn đường

            Collider2D otherBody = other.BodyCollider;
            if (otherBody == null) continue;

            // Khoảng hở mép-với-mép đang có (âm = đang chồng lên nhau), cùng quy ước với GetColliderGap.
            float gap = (otherBody.bounds.center.x - ownCenter) * direction
                        - (RestHalfWidth + other.RestHalfWidth);

            float allowed = gap - allyQueueGap;
            if (allowed < offset) offset = allowed;
        }

        return offset;
    }

    /// <summary>
    /// Khoảng trống phía trước (tính từ MÉP THÂN mình) trước khi chạm đồng minh đi trước (chừa khe
    /// hở allyQueueGap) hoặc chạm địch (dừng khi khe hở còn đúng tầm đánh AttackGapRange).
    /// Dùng để giới hạn cú nhảy. Tất cả đều đo trên MÉP THÂN chứ không đo từ tâm, vì thân lính rất
    /// to (5-11 world unit): đo từ tâm thì với SpearMan phải sát địch mới "thấy", còn với tầm nhỏ thì
    /// vòng quét nằm lọt trong thân.
    /// </summary>
    protected float GetClearDistanceAhead()
    {
        // Tầm quét phải phủ hết tầm đánh, nếu không lính tầm xa (Catapult bắn 9 unit ngoài mép thân)
        // sẽ không "thấy" địch từ xa để dừng lại đúng tầm mà cứ lao vào sát mới đánh.
        float maxProbe = Mathf.Max(6f, AttackGapRange + 2f);
        Vector2 direction = blueSide ? Vector2.right : Vector2.left;

        Collider2D own = BodyCollider;
        float originX = (own != null)
            ? (blueSide ? own.bounds.max.x : own.bounds.min.x)
            : transform.position.x;

        // Quét bắt đầu từ MÉP TRƯỚC của thân nên tầm "nhìn" không bị thân to ăn hết.
        RaycastHit2D[] hits = Physics2D.CircleCastAll(new Vector2(originX, transform.position.y), allyCheckRadius, direction, maxProbe);
        float clear = maxProbe;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.gameObject == gameObject) continue;

            Slime other = hit.collider.GetComponentInParent<Slime>();
            if (other == null || other == this) continue;   // cổng / tường / đạn...: bỏ qua

            if (other.blueSide != blueSide)
            {
                // ĐỊCH: quãng còn phải đi cho tới chỗ đứng (khe hở = tầm đánh), nhưng chỉ nhích
                // MỘT NỬA quãng đó. Vì địch cũng đang tiến về phía ta: nếu cả 2 cùng nhích hết cỡ
                // trong cùng một frame thì tổng quãng gấp đôi => đâm xuyên qua nhau, rồi bị chốt
                // chặn đẩy ngược lại => cà giật và văng. Nhích nửa thì 2 bên gặp nhau đúng chỗ đứng.
                float enemyGap = GetColliderGap(hit.collider);
                float enemyClear = (enemyGap - AttackGapRange) * 0.5f;
                if (enemyClear < clear) clear = enemyClear;
                continue;
            }

            // ĐỒNG MINH: chỉ bỏ qua King cùng phe (xem AllyBlocksMe - mọi lính cùng phe đều chắn
            // đường nhau nên hàng đợi đánh lần lượt, không ai đi xuyên qua ai).
            if (!AllyBlocksMe(other)) continue;

            // Chỉ nhích thêm khi khe hở giữa 2 MÉP THÂN còn > allyQueueGap, nhờ vậy con to
            // (Catapult rộng ~10 unit) cũng không đè lên con phía trước.
            float allyGap = GetColliderGap(hit.collider);
            float allowed = allyGap - allyQueueGap;
            if (allowed < clear) clear = allowed;
        }

        return Mathf.Max(0f, clear);
    }

    // ==========================================
    // ĐO KHOẢNG HỞ GIỮA 2 THÂN (2 phe không đè lên nhau)
    //
    // Lính trong game thân rất to (Sword ~5.2, SpearMan ~11.6, Catapult ~10.3 world unit).
    // Nếu đo tầm đánh từ TÂM unit như trước thì tầm đánh bị "ăn" hết vào thân, muốn đánh nhau
    // 2 con buộc phải chồng lên nhau. Vì vậy mọi khoảng cách ở đây đều đo giữa 2 MÉP THÂN:
    // 0 = 2 thân vừa chạm nhau, ÂM = đang chồng lên nhau.
    // ==========================================

    /// <summary>
    /// Dung sai khi so khoảng hở với tầm đánh. Từ khi có "ép sát" (xem <see cref="PressForward"/>)
    /// lính bù được hết quãng hụt cuối cùng nên hụt thực tế ≈ 0; để 0.3 cho thoải mái, khỏi lỡ nhịp
    /// một frame là mất mục tiêu.
    /// </summary>
    protected const float AttackGapTolerance = 0.3f;

    /// <summary>
    /// Khoảng hở NGANG giữa thân mình và 1 collider khác:
    /// 0 = 2 thân vừa chạm nhau, ÂM = đang chồng lên nhau, dương = còn cách nhau.
    ///
    /// Đo bằng hiệu 2 TÂM trừ đi nửa bề rộng LÚC NGHỈ của cả 2 thân:
    /// - Không dùng Physics2D.Distance vì nó đọc trạng thái physics, mà transform do DOTween di
    ///   chuyển nên physics có thể còn cũ 1 nhịp (Physics2D.autoSyncTransforms = 0 trong project).
    /// - Không dùng bounds hiện tại vì hiệu ứng nhảy/spawn co giãn collider (0.85x ... 1.3x).
    /// </summary>
    protected float GetColliderGap(Collider2D other)
    {
        Collider2D own = BodyCollider;
        if (own == null || other == null) return float.PositiveInfinity;

        float centerDistance = Mathf.Abs(own.bounds.center.x - other.bounds.center.x);
        return centerDistance - (RestHalfWidth + GetRestHalfWidth(other));
    }

    /// <summary>Nửa bề rộng lúc nghỉ của collider khác (lính thì lấy số đã cache, khỏi bị hiệu ứng co giãn).</summary>
    private float GetRestHalfWidth(Collider2D other)
    {
        if (other == null) return 0f;

        Slime otherSlime = other.GetComponentInParent<Slime>();
        return (otherSlime != null) ? otherSlime.RestHalfWidth : other.bounds.extents.x;
    }

    /// <summary>
    /// Tầm đánh quy đổi sang KHOẢNG HỞ giữa 2 collider: attackRange vốn đo từ tâm nên phần đã "ăn"
    /// vào thân (nửa bề rộng) phải trừ ra. Nếu trừ hết mà vẫn âm (lính cận chiến thân to) thì lấy
    /// enemyMinGap làm mức sàn - 2 bên vẫn đánh được nhau mà không chồng lên nhau.
    /// </summary>
    protected float AttackGapRange
    {
        get
        {
            float rangeFromCenter = attackRange - RestHalfWidth;
            return Mathf.Max(enemyMinGap, rangeFromCenter);
        }
    }

    /// <summary>Đối tượng này có đang trong tầm đánh không (đo trên collider thật).</summary>
    protected bool IsInAttackRange(Collider2D other)
    {
        return GetColliderGap(other) <= AttackGapRange + AttackGapTolerance;
    }

    /// <summary>
    /// Quét mọi collider nằm quanh mình trong khoảng margin (đo từ MÉP thân): hộp bao collider được
    /// nới ra margin về mọi phía. Không dùng OverlapCircleAll ở tâm vì với thân to thì hình tròn
    /// tầm đánh nằm gọn BÊN TRONG thân nên quét không tới địch.
    /// </summary>
    protected Collider2D[] OverlapAround(float margin)
    {
        Collider2D own = BodyCollider;
        if (own == null) return Physics2D.OverlapCircleAll(transform.position, margin);

        Bounds b = own.bounds;
        // Trục X dùng nửa bề rộng LÚC NGHỈ để vùng quét không co lại theo hiệu ứng nhảy.
        Vector2 size = new Vector2((RestHalfWidth + margin) * 2f, (b.extents.y + margin) * 2f);
        return Physics2D.OverlapBoxAll(b.center, size, 0f);
    }

    /// <summary>
    /// Collider này có phải ĐỊCH không? Ưu tiên xét PHE (blueSide) vì tag có thể thiếu/sai;
    /// với vật thể không phải lính (cổng...) thì mới xét theo tag.
    /// </summary>
    protected bool IsEnemyCollider(Collider2D col)
    {
        if (col == null || col.gameObject == gameObject) return false;

        Slime other = col.GetComponentInParent<Slime>();
        if (other != null) return other != this && other.blueSide != blueSide;

        return col.CompareTag(blueSide ? "enemy" : "ally");
    }

    /// <summary>
    /// Đơn vị này có được phép bị DỊCH CHUYỂN VỊ TRÍ để tách khỏi đơn vị khác không
    /// (cả khi bị địch áp sát lẫn khi xếp hàng chồng lên đồng minh).
    /// King đứng yên tại chỗ nên trả về false.
    /// </summary>
    protected virtual bool CanBePushedBackFromEnemy
    {
        get { return true; }
    }

    /// <summary>Khoảng hở mà chốt chặn cuối frame kéo 2 thân về (chỉ khi đã chồng lên nhau thật).</summary>
    private const float OverlapResolveGap = 0.05f;

    /// <summary>Mỗi frame chỉ lùi tối đa chừng này để không bị "văng" đi một phát thật xa.</summary>
    private const float MaxSeparationPerFrame = 0.4f;

    /// <summary>
    /// Chốt chặn cuối frame, CHỈ chạy khi 2 collider địch đã chồng lên nhau thật (khoảng hở âm):
    /// khi đó mới tự lùi ra từ từ. Việc dừng đúng chỗ để đánh đã do GetClearDistanceAhead lo, nên
    /// chốt chặn không tranh giành vị trí với nó (tranh nhau là lính cà giật tại chỗ).
    /// Vẫn cần chốt chặn vì 2 bên cùng lao vào nhau thì điểm đáp đã lên lịch từ trước, không rút lại được.
    /// </summary>
    protected virtual void LateUpdate()
    {
        PushBackFromEnemy();
        SeparateFromAllies();
        UpdateIdleAnimation();
    }

    /// <summary>
    /// Chốt chặn cuối frame cho ĐỒNG MINH: can thiệp khi 2 thân cùng phe gần hơn khe hở xếp hàng
    /// <see cref="allyQueueGap"/> (kể cả khi chưa chồng lên nhau) và chỉ đẩy CHÍNH MÌNH LÙI RA SAU.
    ///
    /// Vì sao cần: <see cref="GetClearDistanceAhead"/> chỉ NGĂN không cho lính lao vào đồng minh,
    /// chứ không SỬA được khi chúng đã quá sát nhau - ví dụ lúc mới spawn, hoặc khi tuyến trước bị
    /// địch đẩy lùi. Trước đây chỉ có chốt chặn cho ĐỊCH nên lính cùng phe đè lên nhau vĩnh viễn.
    ///
    /// Chỉ đẩy khi đồng minh ở PHÍA TRƯỚC mình, nhờ vậy:
    ///   - Lính đang cận chiến với địch không bị đẩy lùi (nó không có đồng minh nào ở trước).
    ///   - Mỗi con chỉ lo khoảng cách với con đứng ngay trước nó, không tranh nhau từng frame.
    ///   - Hướng đẩy (ra sau) ngược với hướng đi (ra trước) nên không giằng co với cú nhảy.
    ///
    /// Riêng LÍNH TẦM XA có một ngoại lệ nhỏ: nếu có lính cận chiến cùng phe đứng CHỒNG hẳn lên nó
    /// (thường do bị địch đẩy lùi) thì nó tự lùi ra, không đứng đè lên nhau.
    /// </summary>
    private void SeparateFromAllies()
    {
        if (isSpawning) return;                  // đang chạy hiệu ứng spawn thì đừng đụng vào
        if (!CanBePushedBackFromEnemy) return;   // King đứng yên tại chỗ

        Collider2D own = BodyCollider;
        if (own == null) return;

        // Vùng quét phải phủ tới đúng khe hở mong muốn (không chỉ vùng chồng lấn), nếu không thì
        // lính chỉ được đẩy ra khỏi nhau chứ không được giữ đúng khe hở xếp hàng -> hàng đợi vẫn chen.
        Collider2D[] hits = OverlapAround(OverlapResolveGap + allyQueueGap);
        if (hits.Length == 0) return;

        float direction = blueSide ? 1f : -1f;
        float myCenterX = own.bounds.center.x;

        float deepestPenetration = 0f;

        foreach (Collider2D col in hits)
        {
            if (col == null || col.gameObject == gameObject) continue;

            Slime other = col.GetComponentInParent<Slime>();
            if (other == null || other == this) continue;
            if (other.blueSide != blueSide) continue;   // địch đã có PushBackFromEnemy lo
            if (other is KingSlime) continue;           // King cùng phe không tính là vật cản

            float gap = GetColliderGap(col);

            // LÍNH TẦM XA TỰ LÙI RA: có lính cận chiến cùng phe đứng CHỒNG hẳn lên mình (thường do
            // địch đẩy lùi) và đã ĐỨNG YÊN HẲN (không còn cú nhảy nào đang bay) thì mình nhích ra,
            // hai bên không đứng đè lên nhau. Cận chiến đang bay thì bỏ qua để khỏi giật.
            //
            // Chỉ nhích khi ĐÃ chồng lên nhau thật (khe hở âm), không nhích chỉ vì đứng gần: nhờ vậy
            // lính tầm xa vẫn bám sát hàng cận chiến nhất có thể, không bị đẩy lùi dần ra ngoài tầm bắn.
            if (IsRangedUnit && !other.IsRangedUnit)
            {
                if (!IsSettledAlly(other)) continue;

                float meleePenetration = -gap;
                if (meleePenetration > deepestPenetration) deepestPenetration = meleePenetration;
                continue;
            }

            if (!AllyBlocksMe(other)) continue;   // chỉ bỏ qua King cùng phe

            // Chỉ lo con ở PHÍA TRƯỚC; con ở phía sau thì tự nó lo khoảng cách với mình.
            if ((col.bounds.center.x - myCenterX) * direction <= 0f) continue;

            float penetration = allyQueueGap - gap;
            if (penetration > deepestPenetration) deepestPenetration = penetration;
        }

        if (deepestPenetration <= 0f) return;

        // Mỗi frame chỉ lùi một quãng ngắn cho mượt, không "văng" một phát thật xa.
        float push = Mathf.Min(deepestPenetration, MaxSeparationPerFrame);

        // Lùi ra xa đồng minh phía trước, giữ nguyên Y để không phá cú nhảy đang bay.
        Vector3 position = transform.position;
        transform.position = new Vector3(position.x - (direction * push), position.y, position.z);
    }

    /// <summary>
    /// Đồng minh này đã đứng yên hẳn chưa: không đi, và không còn cú nhảy nào đang bay.
    /// Dùng để bỏ qua lính cận chiến đang BAY (chồng lên mình chỉ trong chốc lát) và chỉ nhích ra
    /// khi nó đã đứng ì ngay trên mình trong <see cref="SeparateFromAllies"/>.
    /// </summary>
    private static bool IsSettledAlly(Slime other)
    {
        return !other.isMoving && !other.IsMoveTweenRunning;
    }

    private void PushBackFromEnemy()
    {
        if (!CanBePushedBackFromEnemy) return;

        Collider2D own = BodyCollider;
        if (own == null) return;

        Collider2D[] hits = OverlapAround(OverlapResolveGap);
        if (hits.Length == 0) return;

        float deepestPenetration = 0f;
        float deepestEnemyX = 0f;

        foreach (Collider2D col in hits)
        {
            if (!IsEnemyCollider(col)) continue;

            // Chỉ tách LÍNH với LÍNH. Cổng/tường (không có component Slime) không tách: đơn vị
            // vốn được đặt đứng sát cổng theo Destination, tách ra thì nó bị đẩy lùi rồi lại nhảy
            // tới, lặp vô hạn.
            if (col.GetComponentInParent<Slime>() == null) continue;

            // CHỈ can thiệp khi 2 thân THỰC SỰ chồng lên nhau (khoảng hở âm). Không dùng mức
            // enemyMinGap ở đây: nếu chốt chặn cũng giữ đúng khoảng hở mà lính nhắm tới thì hai
            // cơ chế giành nhau từng frame => lính cà giật tại chỗ.
            float penetration = OverlapResolveGap - GetColliderGap(col);
            if (penetration <= deepestPenetration) continue;

            deepestPenetration = penetration;
            deepestEnemyX = col.bounds.center.x;
        }

        if (deepestPenetration <= 0f) return;

        // Mỗi frame chỉ lùi một quãng ngắn cho mượt, không "văng" một phát thật xa.
        float push = Mathf.Min(deepestPenetration, MaxSeparationPerFrame);

        // Lùi ra xa địch theo trục X (game chạy ngang), giữ nguyên Y để không phá cú nhảy đang bay.
        // Xác định bên nào bằng TÂM 2 con (rõ ràng, không phụ thuộc quy ước dấu của vector).
        float pushDirection;
        if (Mathf.Abs(transform.position.x - deepestEnemyX) < 0.01f)
        {
            // Trùng tâm nhau: lùi về phía sau lưng mình, 2 phe cùng lùi thì tách ra chứ không lùi cùng chiều.
            pushDirection = blueSide ? -1f : 1f;
        }
        else
        {
            pushDirection = (transform.position.x > deepestEnemyX) ? 1f : -1f;
        }

        Vector3 position = transform.position;
        transform.position = new Vector3(position.x + pushDirection * push, position.y, position.z);
    }

    public virtual void TakeDamage(int dmg)
    {
        health -= dmg;
        PlayDamageFlash();
        if (health <= 0)
        {
            Die();
        }
    }

    // ==========================================
    // NHẤP NHÁY ĐỎ KHI TRÚNG ĐÒN
    // ==========================================

    /// <summary>
    /// Nhấp nháy đỏ báo unit vừa bị tấn công. Vì được gọi từ TakeDamage nên mọi nguồn sát thương
    /// (cận chiến, đạn, King) đều có phản hồi giống nhau. Nếu đang nhấp nháy mà bị đánh tiếp thì
    /// nhịp cũ bị huỷ và chạy lại từ đầu (không bị kẹt màu đỏ).
    /// </summary>
    protected void PlayDamageFlash()
    {
        if (damageFlashBlinks <= 0 || damageFlashDuration <= 0f) return;
        if (!CacheDamageFlashRenderers()) return;

        if (damageFlashTween != null) damageFlashTween.Kill();

        ApplyDamageFlashColor(0f);   // luôn bắt đầu từ màu gốc

        Sequence flashSeq = DOTween.Sequence();
        for (int i = 0; i < damageFlashBlinks; i++)
        {
            flashSeq.AppendCallback(() => ApplyDamageFlashColor(1f));
            flashSeq.AppendInterval(damageFlashDuration);
            flashSeq.AppendCallback(() => ApplyDamageFlashColor(0f));
            flashSeq.AppendInterval(damageFlashDuration * 0.5f);   // nghỉ ngắn trước nhịp sau
        }

        damageFlashTween = flashSeq;
    }

    /// <summary>Gom các SpriteRenderer của unit và cache màu gốc (chỉ làm 1 lần).</summary>
    private bool CacheDamageFlashRenderers()
    {
        if (damageFlashRenderers != null) return damageFlashRenderers.Length > 0;

        damageFlashRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        damageFlashBaseColors = new Color[damageFlashRenderers.Length];

        for (int i = 0; i < damageFlashRenderers.Length; i++)
        {
            damageFlashBaseColors[i] = damageFlashRenderers[i].color;
        }

        return damageFlashRenderers.Length > 0;
    }

    /// <summary>t = 0: màu gốc, t = 1: màu nhấp nháy.</summary>
    private void ApplyDamageFlashColor(float t)
    {
        if (damageFlashRenderers == null) return;

        for (int i = 0; i < damageFlashRenderers.Length; i++)
        {
            SpriteRenderer sr = damageFlashRenderers[i];
            if (sr == null) continue;   // unit có thể đã bị destroy giữa lúc nhấp nháy

            sr.color = Color.Lerp(damageFlashBaseColors[i], damageFlashColor, t);
        }
    }

    protected virtual void Die()
    {
        if (moveTween != null) moveTween.Kill();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.soldierDown(characterName);
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (moveTween != null) moveTween.Kill();
        if (idleTween != null) idleTween.Kill();
        if (damageFlashTween != null) damageFlashTween.Kill();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 rayDir = blueSide ? Vector3.right : Vector3.left;
        Vector3 checkPos = transform.position + rayDir * GetAllyCheckDistance(allyCheckDistance);
        Gizmos.DrawWireSphere(checkPos, allyCheckRadius);
    }

    private void PlaySpawnPopEffect()
    {
        isSpawning = true;
        isMoving = true;

        float direction = blueSide ? 1f : -1f;
        Vector3 startPos = transform.position;

        // Cú lao tới lúc mới spawn phải dừng lại đúng CUỐI HÀNG đồng minh, nếu không lính mới sẽ đáp
        // thẳng vào con đứng cuối (thân rộng 5-10 đơn vị mà cú lao dài 4 đơn vị nên chắc chắn chồng
        // lên nhau). Khi hàng đã lấn tới/qua điểm spawn thì quãng này ÂM: lính mới lùi ra sau hàng.
        float popDistance = GetQueueSlotOffset(spawnDashDistance);

        // Khi TIẾN LÊN thì không được lao lố qua điểm đến; khi phải LÙI ra sau hàng thì không cần
        // giới hạn theo điểm đến (điểm đến luôn ở phía trước).
        if (popDistance > 0f)
        {
            float distanceToDest = Mathf.Abs(transform.position.x - Destination.x);
            popDistance = Mathf.Min(popDistance, Mathf.Max(0f, distanceToDest - arriveDistance));
        }

        float popHeight = 5.0f;

        Vector3 landPos = new Vector3(startPos.x + (popDistance * direction), startPos.y, startPos.z);
        transform.localScale = Vector3.zero;

        Sequence spawnSeq = DOTween.Sequence();

        spawnSeq.Append(transform.DOScale(originalScale * 1.1f, 0.25f).SetEase(Ease.OutBack));

        spawnSeq.Join(transform.DOMoveX(landPos.x, 0.5f).SetEase(Ease.Linear));
        spawnSeq.Join(transform.DOScale(new Vector3(originalScale.x * 0.9f, originalScale.y * 1.3f, originalScale.z), 0.25f).SetEase(Ease.OutQuad));

        Sequence popYSeq = DOTween.Sequence();
        popYSeq.Append(transform.DOMoveY(startPos.y + popHeight, 0.25f).SetEase(Ease.OutQuad));
        popYSeq.Append(transform.DOMoveY(landPos.y, 0.25f).SetEase(Ease.InQuad));

        spawnSeq.Join(popYSeq);

        spawnSeq.Append(transform.DOScale(new Vector3(originalScale.x * 1.35f, originalScale.y * 0.65f, originalScale.z), 0.1f));

        spawnSeq.Append(transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack));

        spawnSeq.OnComplete(() => {
            isSpawning = false;
            SnapToGround();       // luôn đáp đúng mặt nền, không treo trên không
            isMoving = false;     // để Update tự quyết định đi tiếp hay đứng lại đánh
        });
    }

    // ==========================================
    // IDLE ANIMATION - HIỆU ỨNG "THỞ" LÚC ĐỨNG YÊN CHỜ
    //
    // Lính đứng chờ (chờ hàng tiến lên, chờ vào tầm đánh) trước đây đứng im hoàn toàn nên nhìn
    // như tượng. Hiệu ứng này chỉ co giãn nhẹ transform (DOTween) nên KHÔNG đụng tới vị trí:
    // không phá việc đứng trên mặt đất, không giành vị trí với cú nhảy hay chốt chặn giãn cách.
    // ==========================================

    /// <summary>
    /// Lính có đang ở trạng thái "đứng yên chờ" để chạy hiệu ứng thở không.
    /// Các lớp con không cần gọi gì thêm: chúng vốn đã set <see cref="isMoving"/> và
    /// <see cref="target"/> đúng như lúc đi/đánh nên chỉ cần đọc 2 cờ đó.
    /// </summary>
    protected virtual bool ShouldPlayIdleAnimation
    {
        get
        {
            if (!enableIdleAnimation) return false;
            if (isSpawning || isMoving) return false;      // đang hiệu ứng spawn / đang nhảy
            if (IsMoveTweenRunning) return false;          // cú nhảy đang bay (isMoving đã tắt nhưng tween còn chạy)
            if (target != null) return false;              // đang trong tầm đánh -> đánh, không tính là chờ
            return CanPlayIdleAnimation;
        }
    }

    /// <summary>Cú nhảy/xe chạy có đang bay không (tween còn hoạt động).</summary>
    protected bool IsMoveTweenRunning
    {
        get { return moveTween != null && moveTween.IsActive(); }
    }

    /// <summary>
    /// Lớp con nào KHÔNG muốn hiệu ứng thở thì trả về false (King đứng yên cả trận, có animator
    /// riêng và bị DOPunchScale khi trúng đòn nên để yên).
    /// </summary>
    protected virtual bool CanPlayIdleAnimation
    {
        get { return true; }
    }

    /// <summary>
    /// Bật/tắt hiệu ứng thở theo trạng thái thật của lính, gọi mỗi frame ở <see cref="LateUpdate"/>.
    /// Đặt ở class cha để mọi loại lính (Sword, Archer, Catapult...) dùng chung một logic.
    /// </summary>
    protected void UpdateIdleAnimation()
    {
        if (ShouldPlayIdleAnimation) StartIdleAnimation();
        else StopIdleAnimation();
    }

    /// <summary>Chạy nhịp thở lặp vô hạn (chỉ đụng vào scale, luôn lấy originalScale làm mốc).</summary>
    protected virtual void StartIdleAnimation()
    {
        if (idleTween != null && idleTween.IsActive()) return;   // đang thở rồi

        // Mỗi con một nhịp hơi khác nhau để cả hàng không thở y hệt nhau như máy.
        float breath = Mathf.Max(0.1f, idleBreathDuration) * Random.Range(0.85f, 1.15f);
        float half = breath * 0.5f;
        float squash = Mathf.Clamp(idleSquashAmount, 0f, 0.4f);

        Vector3 squashed = new Vector3(originalScale.x * (1f + squash),
                                       originalScale.y * (1f - squash),
                                       originalScale.z);
        Vector3 stretched = new Vector3(originalScale.x * (1f - squash * 0.5f),
                                        originalScale.y * (1f + squash * 0.5f),
                                        originalScale.z);

        Sequence breathSeq = DOTween.Sequence();

        // Nhịp thở (co giãn) + RƯỚN NGƯỜI về phía trước: lính đứng chờ nghiêng hẳn về phía địch rồi
        // trả về, nhìn như đang muốn lao vào đánh chứ không phải đứng im như tượng.
        float lean = Mathf.Clamp(idleLeanAngle, 0f, 20f) * (blueSide ? -1f : 1f);   // nghiêng về phía trước
        Vector3 leanForward = new Vector3(0f, 0f, lean);
        Vector3 leanBack = new Vector3(0f, 0f, lean * 0.35f);

        breathSeq.Append(transform.DOScale(squashed, half).SetEase(Ease.InOutSine));
        breathSeq.Join(transform.DOLocalRotate(leanForward, half).SetEase(Ease.InOutSine));
        breathSeq.Append(transform.DOScale(stretched, half).SetEase(Ease.InOutSine));
        breathSeq.Join(transform.DOLocalRotate(leanBack, half).SetEase(Ease.InOutSine));
        breathSeq.Append(transform.DOScale(originalScale, half * 0.7f).SetEase(Ease.InOutSine));
        breathSeq.Join(transform.DOLocalRotateQuaternion(originalRotation, half * 0.7f).SetEase(Ease.InOutSine));
        breathSeq.SetLoops(-1, LoopType.Restart);

        idleTween = breathSeq;
    }

    /// <summary>
    /// Tắt hiệu ứng thở/rướn người và trả lại đúng tỉ lệ + góc gốc. Chỉ trả scale khi không có hiệu
    /// ứng khác đang điều khiển scale (cú nhảy / hiệu ứng spawn) để 2 bên không giành nhau.
    /// </summary>
    protected void StopIdleAnimation()
    {
        if (idleTween == null) return;

        idleTween.Kill();
        idleTween = null;

        if (!isSpawning && !IsMoveTweenRunning) transform.localScale = originalScale;
        transform.localRotation = originalRotation;
    }

    /// <summary>
    /// Vẽ vùng tầm đánh THẬT (hộp bao collider nở ra AttackGapRange) khi chọn unit trong Editor.
    /// Đây mới là khoảng cách mà lính dừng lại để đánh - chỉnh enemyMinGap sẽ thấy hộp này đổi.
    /// </summary>
    protected void DrawAttackGizmo(Color color)
    {
        Collider2D own = BodyCollider;
        if (own == null) return;

        Bounds b = own.bounds;
        Vector2 size = new Vector2(b.size.x, b.size.y) + (Vector2.one * (AttackGapRange * 2f));

        Gizmos.color = color;
        Gizmos.DrawWireCube(b.center, size);
    }

    /// <summary>
    /// Tìm ĐỊCH gần nhất đang trong tầm đánh. Khoảng cách đo trên COLLIDER THẬT (mép với mép), gần
    /// nhưng KHÔNG đè lên nhau. Vùng quét cũng tính từ collider của mình nới ra tầm đánh, nên lính
    /// thân to cũng quét tới địch (trước đây quét hình tròn ở tâm nên không tới được địch).
    /// </summary>
    protected virtual void FindTarget()
    {
        // Vùng quét phải phủ hết "vùng được coi là trong tầm đánh".
        float searchMargin = Mathf.Max(attackRange, AttackGapRange + AttackGapTolerance);
        Collider2D[] hits = OverlapAround(searchMargin);

        target = null;
        float closestEnemyGap = Mathf.Infinity;

        foreach (Collider2D col in hits)
        {
            if (!IsEnemyCollider(col)) continue;
            if (!IsInAttackRange(col)) continue;

            float gap = GetColliderGap(col);
            if (gap < closestEnemyGap)
            {
                closestEnemyGap = gap;
                target = col.gameObject;
            }
        }
    }

    /// <summary>
    /// Đảm bảo lính LUÔN có mục tiêu trong tầm để đánh tiếp: nếu mục tiêu hiện tại đã hết hợp lệ
    /// (chết, hoặc đi ra khỏi tầm) thì TÌM NGAY một mục tiêu khác còn trong tầm.
    ///
    /// Dùng cho lính TẦM XA: chỉ cần còn 1 địch trong tầm là bắn tiếp, không phải ngắt đòn rồi mới
    /// bắn lại. Nếu gọi StopAttack (như trước đây) thì lính bị đưa về trạng thái đi bộ - nhảy lên
    /// một cái rồi mới phát hiện mục tiêu mới, nhìn như đang "chờ tới lượt". Nhờ hàm này lính xa
    /// đứng trong hàng đợi (sau lưng đồng minh) vẫn bắn đều vào bất kỳ địch nào lọt vào tầm.
    ///
    /// Trả về true nếu vẫn còn mục tiêu để đánh.
    /// </summary>
    protected bool EnsureTargetStillValid()
    {
        if (target != null && IsValidTarget(target)) return true;

        FindTarget();
        return target != null;
    }

    /// <summary>Mục tiêu này còn trong tầm đánh không (đo trên collider thật).</summary>
    protected virtual bool IsValidTarget(GameObject t)
    {
        if (t == null) return false;

        Collider2D col = t.GetComponent<Collider2D>();
        if (col != null) return IsInAttackRange(col);

        // Không có collider thì đành đo từ tâm như cũ.
        return Vector2.Distance(transform.position, t.transform.position) <= attackRange;
    }
}