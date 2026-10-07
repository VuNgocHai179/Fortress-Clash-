"""
Kiểm tra nhanh model ONNX đã train: nạp model + scaler, in thông tin và thử vài thế trận.

Chạy từ thư mục gốc project:
    python test_onnx.py

PHỤ THUỘC: numpy + onnxruntime (không cần pandas / scikit-learn / joblib như bản cũ).
Scaler được đọc từ fortress_ai_scaler.json do train.py sinh ra.
"""

import json
import os
import sys

# Console Windows mặc định dùng cp1252 nên in tiếng Việt sẽ lỗi UnicodeEncodeError.
for stream in (sys.stdout, sys.stderr):
    try:
        stream.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

import numpy as np
import onnxruntime as ort

ONNX_MODEL = "Assets/Scripts/NPC/fortress_ai_model.onnx"
SCALER_PATH = "fortress_ai_scaler.json"

# Đơn giá lính đỏ — PHẢI khớp Assets/Prefab/Red/*.prefab.
PRICES = [30, 40, 20, 50, 10, 60]


def load_scaler(path):
    if not os.path.isfile(path):
        sys.exit(
            f"Không tìm thấy '{path}'.\n"
            "Hãy chạy `python train.py` trước (script đó sinh ra file này)."
        )

    with open(path, encoding="utf-8") as f:
        meta = json.load(f)

    return (
        np.array(meta["mean"], dtype=np.float32),
        np.array(meta["scale"], dtype=np.float32),
        meta["features"],
        meta["actions"],
    )


def main():
    if not os.path.isfile(ONNX_MODEL):
        sys.exit(f"Không tìm thấy '{ONNX_MODEL}'.")

    mean, scale, feature_names, actions = load_scaler(SCALER_PATH)

    session = ort.InferenceSession(ONNX_MODEL, providers=["CPUExecutionProvider"])
    input_info = session.get_inputs()[0]

    print("=" * 60)
    print("ONNX")
    print("=" * 60)
    print("Input :", input_info.name, input_info.shape, input_info.type)
    for output in session.get_outputs():
        print("Output:", output.name, output.shape, output.type)

    print("\nThứ tự đặc trưng (PHẢI khớp AI.BuildFeatureVector trong AI.cs):")
    for i, name in enumerate(feature_names):
        print(f"  {i:2d} -> {name}")

    if len(feature_names) != 19:
        sys.exit(f"Scaler có {len(feature_names)} đặc trưng, cần đúng 19.")

    # ========================================================
    # THỬ VÀI THẾ TRẬN
    # ========================================================

    cases = [
        ([0] * 6, [0] * 6, 0, "WAITING"),
        ([0] * 6, [0] * 6, 10, "CHOOSEBASESLIME"),
        ([0] * 6, [0] * 6, 30, "CHOOSESWORD"),
        ([8, 0, 0, 0, 0, 0], [0] * 6, 100, "CHOOSESPEARMAN (địch nhiều melee -> giáo dài)"),
        ([0, 0, 0, 0, 0, 8], [0] * 6, 100, "CHOOSESWORD (địch nhiều tầm xa -> lao vào)"),
        ([5] * 6, [0, 0, 0, 0, 0, 5], 100, "CHOOSESPEARMAN (mình toàn tầm xa -> thêm melee)"),
        ([5] * 6, [5, 0, 0, 0, 0, 0], 100, "CHOOSECATAPULT (mình toàn melee -> thêm tầm xa)"),
    ]

    print("\n" + "=" * 60)
    print("THỬ THẾ TRẬN")
    print("=" * 60)

    for player, own, money, note in cases:
        raw = np.array([player + own + PRICES + [money]], dtype=np.float32)
        scaled = (raw - mean) / scale
        logits = session.run(None, {input_info.name: scaled})[0][0]

        # index 0 = WAITING luôn hợp lệ, các index sau chỉ hợp lệ khi đủ tiền.
        best_index = 0
        best_score = logits[0]
        for unit, price in enumerate(PRICES):
            if money >= price and logits[1 + unit] > best_score:
                best_index = 1 + unit
                best_score = logits[1 + unit]

        print(f"player{player} ai{own} tiền {money:4} -> {actions[best_index]:16s} | kỳ vọng: {note}")


if __name__ == "__main__":
    main()
