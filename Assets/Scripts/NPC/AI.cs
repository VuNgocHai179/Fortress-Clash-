using Unity.InferenceEngine;
using UnityEngine;

public enum State
{
    WAITING,
    CHOOSESWORD,
    CHOOSEARCHER,
    CHOOSESPEARMAN,
    CHOOSESLINGER,
    CHOOSECATAPULT,
    CHOOSESOLDIER,
}

public class AI : MonoBehaviour
{
    [Header("ONNX Model")]
    public ModelAsset modelAsset;

    private Worker worker;

    // =========================================================
    // START
    // =========================================================

    void Start()
    {
        Model model = ModelLoader.Load(modelAsset);

        worker = new Worker(
            model,
            BackendType.GPUCompute
        );

        Debug.Log("AI Model loaded!");

        State result = Predict(
            5,  // PlayerSword
            2,  // PlayerArcher
            1,  // PlayerSlinger
            3,  // PlayerSpearMan
            4,  // PlayerSoldier
            0,  // PlayerCatapult

            2,  // AI Sword
            1,  // AI Archer
            0,  // AI Slinger
            2,  // AI SpearMan
            3,  // AI Soldier
            0,  // AI Catapult

            500, // AI Sword Money
            500, // AI Archer Money
            500, // AI Slinger Money
            500, // AI SpearMan Money
            500, // AI Catapult Money

            300  // AI Money
        );

        Debug.Log(
            "AI quyết định: " + result
        );
    }


    // =========================================================
    // INFER
    // =========================================================

    public State Predict(
        int playerSwordNumber,
        int playerArcherNumber,
        int playerSlingerNumber,
        int playerSpearManNumber,
        int playerSoldierNumber,
        int playerCatapultNumber,

        int aiSwordNumber,
        int aiArcherNumber,
        int aiSlingerNumber,
        int aiSpearManNumber,
        int aiSoldierNumber,
        int aiCatapultNumber,

        int aiSwordMoney,
        int aiArcherMoney,
        int aiSlingerMoney,
        int aiSpearManMoney,
        int aiCatapultMoney,

        int aiMoney
    )
    {
        // =====================================================
        // 1. TẠO INPUT
        // =====================================================

        float[] inputData =
        {
            playerSwordNumber,
            playerArcherNumber,
            playerSlingerNumber,
            playerSpearManNumber,
            playerSoldierNumber,
            playerCatapultNumber,

            aiSwordNumber,
            aiArcherNumber,
            aiSlingerNumber,
            aiSpearManNumber,
            aiSoldierNumber,
            aiCatapultNumber,

            aiSwordMoney,
            aiArcherMoney,
            aiSlingerMoney,
            aiSpearManMoney,
            aiCatapultMoney,

            aiMoney
        };


        // =====================================================
        // 2. STANDARDIZE INPUT
        // =====================================================

        // TODO:
        // Thay mean/std bằng giá trị lấy từ
        // fortress_ai_scaler.pkl
        //
        // Hiện tại tạm thời chưa chuẩn hóa.
        //
        // Khi có mean/std chính xác, dùng:
        //
        // inputData[i] =
        //     (inputData[i] - mean[i]) / std[i];


        // =====================================================
        // 3. TẠO TENSOR
        // =====================================================

        Tensor<float> inputTensor = new Tensor<float>(
            new TensorShape(1, 18),
            inputData
        );


        // =====================================================
        // 4. CHẠY MODEL
        // =====================================================

        worker.Schedule(
            inputTensor
        );


        // =====================================================
        // 5. LẤY OUTPUT
        // =====================================================

        Tensor<float> outputTensor =
            worker.PeekOutput() as Tensor<float>;


        // =====================================================
        // 6. COPY OUTPUT VỀ CPU
        // =====================================================

        float[] output = outputTensor.DownloadToArray();


        // =====================================================
        // 7. IN OUTPUT
        // =====================================================

        Debug.Log(
            $"AI Output: " +
            $"[0]={output[0]:F4}, " +
            $"[1]={output[1]:F4}, " +
            $"[2]={output[2]:F4}, " +
            $"[3]={output[3]:F4}"
        );


        // =====================================================
        // 8. TÌM OUTPUT LỚN NHẤT
        // =====================================================

        int bestIndex = 0;

        for (int i = 1; i < output.Length; i++)
        {
            if (output[i] > output[bestIndex])
            {
                bestIndex = i;
            }
        }


        // =====================================================
        // 9. GIẢI MÃ ACTION
        // =====================================================

        State result = ConvertIndexToState(
            bestIndex
        );


        Debug.Log(
            $"AI Decision: {result}"
        );


        // =====================================================
        // 10. GIẢI PHÓNG TENSOR
        // =====================================================

        inputTensor.Dispose();
        outputTensor.Dispose();


        return result;
    }


    // =========================================================
    // CONVERT INDEX -> STATE
    // =========================================================

    private State ConvertIndexToState(
        int index
    )
    {
        switch (index)
        {
            case 0:
                return State.CHOOSECATAPULT;

            case 1:
                return State.CHOOSEARCHER;

            case 2:
                return State.CHOOSESPEARMAN;

            case 3:
                return State.CHOOSESWORD;

            default:
                Debug.LogWarning(
                    $"Unknown AI action index: {index}"
                );

                return State.WAITING;
        }
    }


    // =========================================================
    // DESTROY
    // =========================================================

    private void OnDestroy()
    {
        worker?.Dispose();
    }
}