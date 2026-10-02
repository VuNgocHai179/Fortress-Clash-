import torch

from train import FortressAI, make_onnx_self_contained


# ============================================================
# GHI CHÚ
# ============================================================
# train.py giờ đã tự xuất ONNX nên script này chỉ còn dùng khi muốn xuất lại ONNX
# từ file trọng số .pth có sẵn (ví dụ sau khi sửa tay trọng số).
#
# LƯU Ý: phải để external_data=False và gọi make_onnx_self_contained(), nếu không PyTorch sẽ ghi
# trọng số ra file ngoài `<tên>.onnx.data` — Unity chỉ import đúng 1 file .onnx nên sẽ lỗi.


# ============================================================
# CONFIG
# ============================================================

PYTORCH_MODEL = "fortress_ai_model.pth"

ONNX_MODEL = "fortress_ai_model.onnx"

INPUT_SIZE = 19

OUTPUT_SIZE = 7


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

    opset_version=15,

    external_data=False
)


make_onnx_self_contained(ONNX_MODEL)


print("========================================")
print("ONNX EXPORT SUCCESS")
print("========================================")

print(
    "Input size:",
    INPUT_SIZE
)

print(
    "Output size:",
    OUTPUT_SIZE
)

print(
    "Saved:",
    ONNX_MODEL
)