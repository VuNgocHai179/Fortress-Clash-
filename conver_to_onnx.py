import torch

from train import FortressAI


# ============================================================
# CONFIG
# ============================================================

PYTORCH_MODEL = "fortress_ai_model.pth"

ONNX_MODEL = "fortress_ai_model.onnx"

INPUT_SIZE = 18

OUTPUT_SIZE = 4


# ============================================================
# 1. CREATE MODEL
# ============================================================

model = FortressAI(
    INPUT_SIZE,
    OUTPUT_SIZE
)


# ============================================================
# 2. LOAD WEIGHTS
# ============================================================

model.load_state_dict(
    torch.load(
        PYTORCH_MODEL,
        map_location="cpu"
    )
)


model.eval()


# ============================================================
# 3. CREATE DUMMY INPUT
# ============================================================

dummy_input = torch.randn(
    1,
    INPUT_SIZE,
    dtype=torch.float32
)


# ============================================================
# 4. EXPORT ONNX
# ============================================================

torch.onnx.export(

    model,

    dummy_input,

    ONNX_MODEL,

    input_names=[
        "input"
    ],

    output_names=[
        "output"
    ],

    dynamic_axes={

        "input": {
            0: "batch"
        },

        "output": {
            0: "batch"
        }

    },

    opset_version=15
)


print("========================================")
print("ONNX EXPORT SUCCESS")
print("========================================")

print(
    "Saved:",
    ONNX_MODEL
)