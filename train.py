"""
Train model ONNX cho AI phe đỏ từ dataset do AIPolicy sinh ra.

QUY TRÌNH
    1. Trong Unity:  Tools > Fortress Clash > Generate AI Dataset   -> dataset.csv
    2. Chạy:         python train.py                                 -> fortress_ai_model.onnx
    3. Copy fortress_ai_model.onnx đè vào Assets/Scripts/NPC/
    4. Dán 2 mảng ScalerMean / ScalerScale mà script in ra vào Assets/Scripts/NPC/AI.cs
    5. Đặt AI.mode = NeuralNetwork

PHỤ THUỘC: chỉ cần numpy + torch (không cần pandas / scikit-learn / joblib).

VÌ SAO ĐỔI SO VỚI BẢN CŨ
    - Bản cũ dùng pandas + sklearn + joblib, và lưu scaler ra .pkl rồi KHÔNG dùng đến:
      AI.cs hard-code scalerMean = 0 / scalerScale = 1, tức model nhận input chưa chuẩn hoá
      trong khi nó được train trên input đã chuẩn hoá.
    - Dataset cũ là dữ liệu random tổng hợp nên model chỉ học vẹt. Dataset mới do AIPolicy
      sinh ra nên nhãn nhất quán và model học được thật.
"""

import csv
import json
import os
import sys

# Console Windows mặc định dùng cp1252 nên in tiếng Việt sẽ lỗi UnicodeEncodeError.
# Ép stdout/stderr sang UTF-8 để script chạy được trên mọi máy.
for stream in (sys.stdout, sys.stderr):
    try:
        stream.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

import numpy as np
import torch
import torch.nn as nn

# ============================================================
# CONFIG
# ============================================================

DATASET_PATH = "dataset.csv"
MODEL_PATH = "fortress_ai_model.onnx"       # file đem vào Unity
STATE_DICT_PATH = "fortress_ai_model.pth"   # trọng số thô, để tham khảo

BATCH_SIZE = 64
EPOCHS = 300
LEARNING_RATE = 0.001
TEST_RATIO = 0.1
RANDOM_SEED = 42

# Thứ tự nhãn PHẢI khớp AIDatasetGenerator.ActionNames và thứ tự đầu ra của model.
ACTIONS = [
    "WAITING",
    "CHOOSESWORD",
    "CHOOSEARCHER",
    "CHOOSESLINGER",
    "CHOOSESPEARMAN",
    "CHOOSEBASESLIME",
    "CHOOSECATAPULT",
]


# ============================================================
# 1. ĐỌC DATASET
# ============================================================

def load_dataset(path):
    if not os.path.isfile(path):
        sys.exit(
            f"Không tìm thấy '{path}'.\n"
            "Hãy chạy trong Unity: Tools > Fortress Clash > Generate AI Dataset"
        )

    with open(path, newline="", encoding="utf-8") as f:
        reader = csv.reader(f)
        header = next(reader)
        rows = [row for row in reader if row]

    if len(header) < 2:
        sys.exit("dataset.csv không hợp lệ (thiếu cột).")

    feature_names = header[:-1]
    X = np.array([[float(v) for v in row[:-1]] for row in rows], dtype=np.float32)
    labels = [row[-1] for row in rows]

    unknown = sorted(set(labels) - set(ACTIONS))
    if unknown:
        sys.exit(f"Nhãn lạ trong dataset: {unknown}. Có thể dataset cũ — hãy sinh lại trong Unity.")

    y = np.array([ACTIONS.index(name) for name in labels], dtype=np.int64)

    print("=" * 60)
    print("DATASET")
    print("=" * 60)
    print("Rows:", len(X), " Features:", X.shape[1])
    for i, name in enumerate(feature_names):
        print(f"  {i:2d} -> {name}")

    if X.shape[1] != 19:
        sys.exit(f"Dataset phải có 19 đặc trưng, đang có {X.shape[1]}.")

    counts = np.bincount(y, minlength=len(ACTIONS))
    print("\nPhân bố nhãn:")
    for i, name in enumerate(ACTIONS):
        print(f"  {name:16s} {counts[i]:6d}  ({counts[i] / len(y) * 100:5.1f}%)")

    return X, y, feature_names


# ============================================================
# 2. CHIA TRAIN / TEST (stratified, không cần sklearn)
# ============================================================

def stratified_split(X, y, test_ratio, seed):
    rng = np.random.default_rng(seed)
    test_indices = []

    for label in np.unique(y):
        indices = np.flatnonzero(y == label)
        rng.shuffle(indices)
        n_test = max(1, int(round(len(indices) * test_ratio)))
        test_indices.extend(indices[:n_test])

    test_indices = np.array(sorted(test_indices))
    train_mask = np.ones(len(y), dtype=bool)
    train_mask[test_indices] = False

    return X[train_mask], X[test_indices], y[train_mask], y[test_indices]


# ============================================================
# 3. CHUẨN HOÁ (StandardScaler: (x - mean) / scale)
# ============================================================

def fit_scaler(X_train):
    mean = X_train.mean(axis=0).astype(np.float32)
    scale = X_train.std(axis=0).astype(np.float32)
    scale[scale == 0] = 1.0
    return mean, scale


# ============================================================
# 4. MODEL
# ============================================================

class FortressAI(nn.Module):
    def __init__(self, input_size, output_size):
        super().__init__()
        self.network = nn.Sequential(
            nn.Linear(input_size, 64),
            nn.ReLU(),
            nn.Linear(64, 32),
            nn.ReLU(),
            nn.Linear(32, output_size),
        )

    def forward(self, x):
        return self.network(x)


# ============================================================
# 5. TRAIN
# ============================================================

def main():
    X, y, feature_names = load_dataset(DATASET_PATH)

    X_train, X_test, y_train, y_test = stratified_split(X, y, TEST_RATIO, RANDOM_SEED)
    mean, scale = fit_scaler(X_train)

    X_train_t = torch.tensor((X_train - mean) / scale, dtype=torch.float32)
    X_test_t = torch.tensor((X_test - mean) / scale, dtype=torch.float32)
    y_train_t = torch.tensor(y_train, dtype=torch.long)
    y_test_t = torch.tensor(y_test, dtype=torch.long)

    print("\n" + "=" * 60)
    print("SPLIT")
    print("=" * 60)
    print("Train:", len(X_train), " Test:", len(X_test))

    torch.manual_seed(RANDOM_SEED)
    model = FortressAI(X.shape[1], len(ACTIONS))
    criterion = nn.CrossEntropyLoss()
    optimizer = torch.optim.Adam(model.parameters(), lr=LEARNING_RATE)

    dataset = torch.utils.data.TensorDataset(X_train_t, y_train_t)
    loader = torch.utils.data.DataLoader(dataset, batch_size=BATCH_SIZE, shuffle=True)

    print("\n" + "=" * 60)
    print("TRAINING")
    print("=" * 60)

    best_test_accuracy = 0.0
    best_state = None

    for epoch in range(EPOCHS):
        model.train()
        total_loss = 0.0

        for batch_X, batch_y in loader:
            outputs = model(batch_X)
            loss = criterion(outputs, batch_y)

            optimizer.zero_grad()
            loss.backward()
            optimizer.step()

            total_loss += loss.item()

        if (epoch + 1) % 10 == 0:
            model.eval()
            with torch.no_grad():
                train_accuracy = (model(X_train_t).argmax(1) == y_train_t).float().mean().item()
                test_pred = model(X_test_t).argmax(1)
                test_accuracy = (test_pred == y_test_t).float().mean().item()

            print(
                f"Epoch {epoch + 1:3d}/{EPOCHS}"
                f" - Loss: {total_loss / len(loader):.6f}"
                f" - Train acc: {train_accuracy:.4f}"
                f" - Test acc: {test_accuracy:.4f}"
            )

            if test_accuracy >= best_test_accuracy:
                best_test_accuracy = test_accuracy
                best_state = {k: v.detach().clone() for k, v in model.state_dict().items()}

    if best_state is not None:
        model.load_state_dict(best_state)

    # ========================================================
    # 6. ĐÁNH GIÁ
    # ========================================================

    model.eval()
    with torch.no_grad():
        predictions = model(X_test_t).argmax(1).numpy()

    print("\n" + "=" * 60)
    print("RESULT")
    print("=" * 60)
    print(f"Accuracy tốt nhất trên tập test: {best_test_accuracy:.4f}")

    majority = np.bincount(y_test, minlength=len(ACTIONS)).max() / len(y_test)
    print(f"Baseline (đoán theo lớp đông nhất): {majority:.4f}")

    print("\nPer-class:")
    for i, name in enumerate(ACTIONS):
        mask = y_test == i
        if mask.sum() == 0:
            continue
        correct = (predictions[mask] == i).sum()
        print(f"  {name:16s} {correct:5d}/{mask.sum():5d}  ({correct / mask.sum() * 100:5.1f}%)")

    # ========================================================
    # 7. XUẤT ONNX
    # ========================================================

    dummy_input = torch.zeros(1, X.shape[1], dtype=torch.float32)

    export_kwargs = dict(
        input_names=["input"],
        output_names=["output"],
        dynamic_axes={"input": {0: "batch"}, "output": {0: "batch"}},
        opset_version=13,
        external_data=False,
    )

    try:
        # Đường xuất mới (cần gói onnxscript).
        torch.onnx.export(model, dummy_input, MODEL_PATH, **export_kwargs)
    except Exception as error:
        print(f"\nXuất ONNX bằng exporter mới thất bại ({error}).")
        print("Thử lại bằng exporter cũ (cần gói onnx)...")

        try:
            torch.onnx.export(model, dummy_input, MODEL_PATH, dynamo=False, **export_kwargs)
        except Exception as fallback_error:
            sys.exit(
                f"Không xuất được ONNX: {fallback_error}\n"
                "Hãy cài thêm gói cần thiết:  pip install onnxscript     (hoặc)  pip install onnx"
            )

    make_onnx_self_contained(MODEL_PATH)
    torch.save(model.state_dict(), STATE_DICT_PATH)

    print("\n" + "=" * 60)
    print("SAVE")
    print("=" * 60)
    print("ONNX model :", MODEL_PATH, "(copy vào Assets/Scripts/NPC/)")
    print("State dict :", STATE_DICT_PATH)

    # ========================================================
    # 8. IN SCALER ĐỂ DÁN VÀO AI.cs
    # ========================================================

    print("\n" + "=" * 60)
    print("DÁN 2 MẢNG SAU VÀO Assets/Scripts/NPC/AI.cs")
    print("=" * 60)

    print("\nprivate static readonly float[] ScalerMean =")
    print("{")
    print(format_array(mean, feature_names))
    print("};")

    print("\nprivate static readonly float[] ScalerScale =")
    print("{")
    print(format_array(scale, feature_names))
    print("};")

    with open("fortress_ai_scaler.json", "w", encoding="utf-8") as f:
        json.dump(
            {
                "features": feature_names,
                "actions": ACTIONS,
                "mean": [float(v) for v in mean],
                "scale": [float(v) for v in scale],
            },
            f,
            indent=2,
        )

    print("\n(Đã lưu thêm fortress_ai_scaler.json để tham khảo.)")


def make_onnx_self_contained(path):
    """
    Đảm bảo file ONNX chỉ gồm MỘT file duy nhất.

    Exporter mới của PyTorch có thể ghi trọng số ra file ngoài `<path>.data`. Unity chỉ import
    đúng file .onnx nên sẽ lỗi/thiếu trọng số. Hàm này gộp mọi external data trở lại vào 1 file.
    """
    external_path = path + ".data"

    if not os.path.isfile(external_path):
        return

    try:
        import onnx
    except ImportError:
        sys.exit(
            f"Model ONNX đang lưu trọng số ra '{external_path}' (Unity không đọc được).\n"
            "Hãy cài gói onnx để gộp lại:  pip install onnx"
        )

    print(f"\nPhát hiện {external_path} -> gộp trọng số vào 1 file ONNX...")

    model = onnx.load(path)
    onnx.save_model(model, path, save_as_external_data=False)
    os.remove(external_path)

    print("Đã gộp xong.")


def format_array(values, feature_names):
    """In mảng float theo hàng, mỗi hàng 6 phần tử, kèm comment tên đặc trưng."""
    lines = []
    values_per_line = 6

    for start in range(0, len(values), values_per_line):
        chunk = values[start:start + values_per_line]
        numbers = ", ".join(f"{v:.10f}f" for v in chunk)
        names = ", ".join(feature_names[start:start + values_per_line])
        lines.append(f"    {numbers},   // {names}")

    return "\n".join(lines)


if __name__ == "__main__":
    main()
