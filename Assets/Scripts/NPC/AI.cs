using System.Collections;
using Unity.InferenceEngine;
using UnityEngine;

public enum State
{
    WAITING,

    CHOOSESWORD,

    CHOOSEARCHER,

    CHOOSESLINGER,

    CHOOSESPEARMAN,

    CHOOSESOLDIER,

    CHOOSECATAPULT
}


public class AI : MonoBehaviour
{
    // =========================================================
    // CONFIG
    // =========================================================

    private const int INPUT_SIZE = 19;

    private const int OUTPUT_SIZE = 7;

    private const float PREDICT_INTERVAL = 5f;


    // =========================================================
    // ONNX MODEL
    // =========================================================

    [Header("ONNX Model")]
    public ModelAsset modelAsset;

    private Worker worker;


    // =========================================================
    // STANDARD SCALER
    // =========================================================
    //
    // Thứ tự PHẢI giống Python:
    //
    // 0  PlayerSwordNumber
    // 1  PlayerArcherNumber
    // 2  PlayerSlingerNumber
    // 3  PlayerSpearManNumber
    // 4  PlayerSoldierNumber
    // 5  PlayerCatapultNumber
    //
    // 6  AISwordNumber
    // 7  AIArcherNumber
    // 8  AISlingerNumber
    // 9  AISpearManNumber
    // 10 AISoldierNumber
    // 11 AICatapultNumber
    //
    // 12 AISwordMoney
    // 13 AIArcherMoney
    // 14 AISlingerMoney
    // 15 AISpearManMoney
    // 16 AISoldierMoney
    // 17 AICatapultMoney
    //
    // 18 AIMoney
    //
    // CẢ HAI MẢNG PHẢI COPY CHÍNH XÁC
    // từ fortress_ai_scaler.pkl
    // =========================================================

    private readonly float[] scalerMean =
    {
        // PlayerSwordNumber
        0f,

        // PlayerArcherNumber
        0f,

        // PlayerSlingerNumber
        0f,

        // PlayerSpearManNumber
        0f,

        // PlayerSoldierNumber
        0f,

        // PlayerCatapultNumber
        0f,


        // AISwordNumber
        0f,

        // AIArcherNumber
        0f,

        // AISlingerNumber
        0f,

        // AISpearManNumber
        0f,

        // AISoldierNumber
        0f,

        // AICatapultNumber
        0f,


        // AISwordMoney
        0f,

        // AIArcherMoney
        0f,

        // AISlingerMoney
        0f,

        // AISpearManMoney
        0f,

        // AISoldierMoney
        0f,

        // AICatapultMoney
        0f,

        // AIMoney
        0f
    };


    private readonly float[] scalerScale =
    {
        // PlayerSwordNumber
        1f,

        // PlayerArcherNumber
        1f,

        // PlayerSlingerNumber
        1f,

        // PlayerSpearManNumber
        1f,

        // PlayerSoldierNumber
        1f,

        // PlayerCatapultNumber
        1f,


        // AISwordNumber
        1f,

        // AIArcherNumber
        1f,

        // AISlingerNumber
        1f,

        // AISpearManNumber
        1f,

        // AISoldierNumber
        1f,

        // AICatapultNumber
        1f,


        // AISwordMoney
        1f,

        // AIArcherMoney
        1f,

        // AISlingerMoney
        1f,

        // AISpearManMoney
        1f,

        // AISoldierMoney
        1f,

        // AICatapultMoney
        1f,

        // AIMoney
        1f
    };


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (modelAsset == null)
        {
            Debug.LogError(
                "AI ModelAsset is NULL!"
            );

            return;
        }


        Model model = ModelLoader.Load(
            modelAsset
        );


        worker = new Worker(
            model,
            BackendType.GPUCompute
        );


        Debug.Log(
            "AI Model loaded!"
        );


        StartCoroutine(
            PredictAI()
        );
    }


    // =========================================================
    // PREDICT LOOP
    // =========================================================

    private IEnumerator PredictAI()
    {
        while (true)
        {
            yield return new WaitForSeconds(
                PREDICT_INTERVAL
            );


            if (worker == null)
            {
                Debug.LogWarning(
                    "AI Worker is NULL!"
                );

                continue;
            }


            State result = Predict(

                // =============================================
                // PLAYER
                // =============================================

                GameManager.Instance.player.SwordNumber,

                GameManager.Instance.player.ArcherNumber,

                GameManager.Instance.player.SlingerNumber,

                GameManager.Instance.player.SpearManNumber,

                GameManager.Instance.player.SoldierNumber,

                GameManager.Instance.player.CatapultNumber,


                // =============================================
                // AI
                // =============================================

                GameManager.Instance.AI.SwordNumber,

                GameManager.Instance.AI.ArcherNumber,

                GameManager.Instance.AI.SlingerNumber,

                GameManager.Instance.AI.SpearManNumber,

                GameManager.Instance.AI.SoldierNumber,

                GameManager.Instance.AI.CatapultNumber,


                // =============================================
                // AI UNIT PRICES
                // =============================================

                GameManager.Instance.AI.SwordMoney,

                GameManager.Instance.AI.ArcherMoney,

                GameManager.Instance.AI.SlingerMoney,

                GameManager.Instance.AI.SpearManMoney,

                GameManager.Instance.AI.SoldierMoney,

                GameManager.Instance.AI.CatapultMoney,


                // =============================================
                // AI MONEY
                // =============================================

                GameManager.Instance.AI.Money
            );


            Debug.Log(
                $"AI Decision: {result}"
            );


            AIAction(
                result
            );
        }
    }


    // =========================================================
    // AI ACTION
    // =========================================================

    private void AIAction(
        State result
    )
    {
        switch (result)
        {
            case State.WAITING:

                Debug.Log(
                    "AI is WAITING"
                );

                return;


            case State.CHOOSESWORD:

                GameManager.Instance
                    .RedSwordChoosen();

                return;


            case State.CHOOSEARCHER:

                GameManager.Instance
                    .RedArcherChoosen();

                return;


            case State.CHOOSESLINGER:

                GameManager.Instance
                    .RedSlingerChoosen();

                return;


            case State.CHOOSESPEARMAN:

                GameManager.Instance
                    .RedSpearManChoosen();

                return;


            case State.CHOOSESOLDIER:

                GameManager.Instance
                    .RedSoldierChoosen();

                return;


            case State.CHOOSECATAPULT:

                GameManager.Instance
                    .RedCatapultChoosen();

                return;


            default:

                Debug.LogWarning(
                    $"Unknown state: {result}"
                );

                return;
        }
    }


    // =========================================================
    // INFERENCE
    // =========================================================

    public State Predict(

        // =============================================
        // PLAYER
        // =============================================

        int playerSwordNumber,

        int playerArcherNumber,

        int playerSlingerNumber,

        int playerSpearManNumber,

        int playerSoldierNumber,

        int playerCatapultNumber,


        // =============================================
        // AI
        // =============================================

        int aiSwordNumber,

        int aiArcherNumber,

        int aiSlingerNumber,

        int aiSpearManNumber,

        int aiSoldierNumber,

        int aiCatapultNumber,


        // =============================================
        // AI UNIT PRICES
        // =============================================

        int aiSwordMoney,

        int aiArcherMoney,

        int aiSlingerMoney,

        int aiSpearManMoney,

        int aiSoldierMoney,

        int aiCatapultMoney,


        // =============================================
        // AI MONEY
        // =============================================

        int aiMoney
    )
    {
        // =====================================================
        // 1. RAW INPUT
        // =====================================================

        float[] inputData =
        {
            // PLAYER

            playerSwordNumber,

            playerArcherNumber,

            playerSlingerNumber,

            playerSpearManNumber,

            playerSoldierNumber,

            playerCatapultNumber,


            // AI

            aiSwordNumber,

            aiArcherNumber,

            aiSlingerNumber,

            aiSpearManNumber,
            
            aiSoldierNumber,

            aiCatapultNumber,


            // AI MONEY / PRICE

            aiSwordMoney,

            aiArcherMoney,

            aiSlingerMoney,

            aiSpearManMoney,

            aiSoldierMoney,

            aiCatapultMoney,

            aiMoney
        };


        // =====================================================
        // 2. CHECK INPUT SIZE
        // =====================================================

        if (inputData.Length != INPUT_SIZE)
        {
            Debug.LogError(
                $"Input size is {inputData.Length}, expected {INPUT_SIZE}"
            );

            return State.WAITING;
        }


        if (scalerMean.Length != INPUT_SIZE ||
            scalerScale.Length != INPUT_SIZE)
        {
            Debug.LogError(
                "Scaler size is invalid!"
            );

            return State.WAITING;
        }


        // =====================================================
        // 3. STANDARDIZE
        // =====================================================

        float[] scaledInput =
            new float[INPUT_SIZE];


        for (int i = 0; i < INPUT_SIZE; i++)
        {
            if (scalerScale[i] == 0f)
            {
                Debug.LogError(
                    $"Scaler scale at index {i} is 0!"
                );

                return State.WAITING;
            }


            scaledInput[i] =
                (inputData[i] - scalerMean[i])
                /
                scalerScale[i];
        }


        // =====================================================
        // 4. CREATE TENSOR
        // =====================================================

        Tensor<float> inputTensor =
            new Tensor<float>(
                new TensorShape(
                    1,
                    INPUT_SIZE
                ),
                scaledInput
            );


        // =====================================================
        // 5. RUN MODEL
        // =====================================================

        worker.Schedule(
            inputTensor
        );


        // =====================================================
        // 6. GET OUTPUT
        // =====================================================

        Tensor<float> outputTensor =
            worker.PeekOutput()
            as Tensor<float>;


        if (outputTensor == null)
        {
            Debug.LogError(
                "ONNX output is null!"
            );

            inputTensor.Dispose();

            return State.WAITING;
        }


        // =====================================================
        // 7. DOWNLOAD OUTPUT
        // =====================================================

        float[] output =
            outputTensor.DownloadToArray();


        // =====================================================
        // 8. CHECK OUTPUT
        // =====================================================

        if (output.Length != OUTPUT_SIZE)
        {
            Debug.LogError(
                $"Model output size is {output.Length}, expected {OUTPUT_SIZE}"
            );

            inputTensor.Dispose();

            outputTensor.Dispose();

            return State.WAITING;
        }


        // =====================================================
        // 9. PRINT LOGITS
        // =====================================================

        Debug.Log(
            "AI Output:\n" +

            $"0 WAITING        = {output[0]:F4}\n" +

            $"1 CHOOSESWORD    = {output[1]:F4}\n" +

            $"2 CHOOSEARCHER   = {output[2]:F4}\n" +

            $"3 CHOOSESLINGER  = {output[3]:F4}\n" +

            $"4 CHOOSESPEARMAN = {output[4]:F4}\n" +

            $"5 CHOOSESOLDIER  = {output[5]:F4}\n" +

            $"6 CHOOSECATAPULT = {output[6]:F4}"
        );


        // =====================================================
        // 10. FILTER ACTIONS BY MONEY
        // =====================================================
        //
        // WAITING luôn hợp lệ.
        //
        // Các unit chỉ hợp lệ khi:
        //
        // AIMoney >= UnitMoney
        //
        // Nếu model muốn chọn unit nhưng không đủ tiền,
        // action đó sẽ bị loại khỏi danh sách.
        // =====================================================

        bool[] validAction =
        {
            true, // WAITING

            aiMoney >= aiSwordMoney,

            aiMoney >= aiArcherMoney,

            aiMoney >= aiSlingerMoney,

            aiMoney >= aiSpearManMoney,

            aiMoney >= aiSoldierMoney,

            aiMoney >= aiCatapultMoney
        };


        Debug.Log(
            "AI Valid Actions:\n" +

            $"WAITING        = {validAction[0]}\n" +

            $"CHOOSESWORD    = {validAction[1]}\n" +

            $"CHOOSEARCHER   = {validAction[2]}\n" +

            $"CHOOSESLINGER  = {validAction[3]}\n" +

            $"CHOOSESPEARMAN = {validAction[4]}\n" +

            $"CHOOSESOLDIER  = {validAction[5]}\n" +

            $"CHOOSECATAPULT = {validAction[6]}"
        );


        // =====================================================
        // 11. FIND BEST VALID ACTION
        // =====================================================

        int bestIndex = 0;

        float bestScore = float.NegativeInfinity;


        for (int i = 0; i < OUTPUT_SIZE; i++)
        {
            // Action không hợp lệ → bỏ qua

            if (!validAction[i])
            {
                continue;
            }


            // Action hợp lệ và có score cao hơn

            if (output[i] > bestScore)
            {
                bestScore = output[i];

                bestIndex = i;
            }
        }


        // =====================================================
        // 12. CONVERT INDEX -> STATE
        // =====================================================

        State result =
            ConvertIndexToState(
                bestIndex
            );


        Debug.Log(
            $"AI Final Decision: {result}" +
            $" | Score: {bestScore:F4}" +
            $" | AIMoney: {aiMoney}"
        );


        // =====================================================
        // 13. DISPOSE
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
            // Python:
            // 0 -> WAITING

            case 0:

                return State.WAITING;


            // Python:
            // 1 -> CHOOSESWORD

            case 1:

                return State.CHOOSESWORD;


            // Python:
            // 2 -> CHOOSEARCHER

            case 2:

                return State.CHOOSEARCHER;


            // Python:
            // 3 -> CHOOSESLINGER

            case 3:

                return State.CHOOSESLINGER;


            // Python:
            // 4 -> CHOOSESPEARMAN

            case 4:

                return State.CHOOSESPEARMAN;


            // Python:
            // 5 -> CHOOSESOLDIER

            case 5:

                return State.CHOOSESOLDIER;


            // Python:
            // 6 -> CHOOSECATAPULT

            case 6:

                return State.CHOOSECATAPULT;


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
        if (worker != null)
        {
            worker.Dispose();

            worker = null;
        }
    }
}