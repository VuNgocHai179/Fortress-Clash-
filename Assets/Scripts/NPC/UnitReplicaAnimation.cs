using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ở CLIENT: chạy animation ĐÁNH NHAU cho lính là bản sao của host.
///
/// VÌ SAO CẦN: client KHÔNG mô phỏng lính - ở client mỗi lính bị tắt hẳn component <see cref="Slime"/>
/// (xem <see cref="Slime.Awake"/>) và chỉ hiển thị vị trí do NetworkTransform đồng bộ về. Nhưng animation
/// của lính lại được bật/tắt từ CHÍNH logic đó (mỗi lớp lính gọi <c>animator.SetBool("isAttack", ...)</c>
/// khi bắt đầu/kết thúc đánh), nên ở client chẳng có gì chạy cả: lính trượt tới rồi đứng im, trong khi 2
/// bên vẫn đang trừ máu nhau. Đó là lý do máy client chỉ thấy sát thương mà không thấy cảnh đánh nhau.
///
/// CÁCH LÀM: mỗi nhịp, hỏi từng lính bản sao "có địch nào trong tầm đánh không" (<see
/// cref="Slime.HasEnemyInAttackRange"/> - cùng luật đo với lúc host tìm mục tiêu) rồi bật/tắt animation
/// đánh cho khớp. Đây thuần là HÌNH ẢNH: client không gây sát thương, không bắn đạn (xem
/// <see cref="Slime.CanApplyAttackEffects"/>) - mọi thứ gameplay vẫn do host chốt.
///
/// KHÔNG CẦN SỬA SCENE: component tự tạo chính mình ở máy client khi vào scene chơi (giống
/// <c>AbilityBar.Bootstrap</c>) và tự huỷ ở host/chơi đơn - nơi lính tự chạy animation của chính nó.
/// </summary>
public class UnitReplicaAnimation : MonoBehaviour
{
    /// <summary>
    /// Bao lâu quét lại danh sách lính + trạng thái đánh một lần (giây). Lính chỉ đánh khi đã áp sát nên
    /// nhịp này là đủ mượt, không cần chạy mỗi frame - và cũng đỡ tốn vì mỗi nhịp phải quét collider.
    /// </summary>
    [Min(0.02f)] [SerializeField] private float refreshInterval = 0.1f;

    /// <summary>Tên tham số bool điều khiển animation đánh trong MỌI animator của lính.</summary>
    private const string AttackParameter = "isAttack";

    /// <summary>Một lính bản sao: component lính (đang bị tắt) + animator của nó.</summary>
    private class Replica
    {
        public Slime slime;
        public Animator animator;
    }

    private readonly List<Replica> replicas = new List<Replica>();
    private float refreshTimer;

    /// <summary>
    /// Tự tạo khi vào scene chơi, y như <c>AbilityBar.Bootstrap</c> - nhờ vậy không phải sửa scene nào và
    /// host/chơi đơn không có object này.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreate();
    }

    private static void TryCreate()
    {
        if (!NetworkRole.IsClientOnly) return;      // host / chơi đơn: lính tự chạy animation của nó
        if (GameManager.Instance == null) return;   // scene không phải màn chơi
        if (FindFirstObjectByType<UnitReplicaAnimation>() != null) return;

        new GameObject("UnitReplicaAnimation").AddComponent<UnitReplicaAnimation>();
    }

    private void Update()
    {
        // Máy này thôi làm client (thoát trận, về menu...) thì không còn việc gì.
        if (!NetworkRole.IsClientOnly)
        {
            Destroy(gameObject);
            return;
        }

        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f) return;

        refreshTimer = Mathf.Max(0.02f, refreshInterval);

        RefreshReplicas();
        RefreshAttackAnimations();
    }

    /// <summary>
    /// Gom lại danh sách lính BẢN SAO: lính do host sinh ra rồi đồng bộ sang, tức là lính có component
    /// <see cref="Slime"/> đã bị tắt. Lính còn chạy được logic (host/chơi đơn) tự lo animation nên bỏ qua.
    /// </summary>
    private void RefreshReplicas()
    {
        replicas.Clear();

        foreach (Slime slime in FindObjectsByType<Slime>(FindObjectsSortMode.None))
        {
            if (slime == null) continue;

            // King có ở CẢ 2 máy và tự chạy animation của mình (xem KingSlime) - không đụng vào.
            if (slime is KingSlime) continue;

            // Còn chạy được logic nghĩa là không phải bản sao.
            if (slime.enabled) continue;

            Animator animator = slime.GetComponent<Animator>();
            if (animator == null) continue;

            replicas.Add(new Replica { slime = slime, animator = animator });
        }
    }

    /// <summary>Bật/tắt animation đánh của từng lính bản sao cho khớp diễn biến đang thấy trên sân.</summary>
    private void RefreshAttackAnimations()
    {
        for (int i = 0; i < replicas.Count; i++)
        {
            Replica replica = replicas[i];

            // Lính có thể đã bị host xoá giữa 2 lần quét (danh sách chỉ được làm mới ở RefreshReplicas).
            if (replica.slime == null || replica.animator == null) continue;

            bool attacking = replica.slime.HasEnemyInAttackRange();

            // So với giá trị ĐANG CÓ trên animator, KHÔNG nhớ trong Replica: danh sách Replica được dựng
            // lại mỗi nhịp (xem RefreshReplicas) nên trạng thái nhớ được luôn quay về false, khi đó lính
            // thôi đánh sẽ không bao giờ được ghi false và kẹt ở animation đánh mãi - vừa đi vừa vung tay.
            // Đọc thẳng animator thì nguồn sự thật luôn đúng và vẫn không ghi thừa mỗi nhịp.
            if (replica.animator.GetBool(AttackParameter) == attacking) continue;

            replica.animator.SetBool(AttackParameter, attacking);
        }
    }
}
