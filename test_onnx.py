import numpy as np
import pandas as pd
import joblib
import onnxruntime as ort


# ============================================================
# LOAD DATA
# ============================================================

df = pd.read_csv("dataset.csv")

X = df.drop(
    ["Action", "State"],
    axis=1
)

X = X.astype(np.float32)


# ============================================================
# LOAD SKLEARN MODEL
# ============================================================

model = joblib.load(
    "fortress_ai_model.pkl"
)


# ============================================================
# LOAD ONNX
# ============================================================

session = ort.InferenceSession(
    "fortress_ai_model.onnx",
    providers=["CPUExecutionProvider"]
)


# ============================================================
# INPUT / OUTPUT
# ============================================================

input_name = session.get_inputs()[0].name

outputs = session.get_outputs()

print("========================================")
print("ONNX")
print("========================================")

print("Input:")
print(
    input_name,
    session.get_inputs()[0].shape,
    session.get_inputs()[0].type
)

print("\nOutputs:")

for output in outputs:

    print(
        output.name,
        output.shape,
        output.type
    )


# ============================================================
# TEST 1 SAMPLE
# ============================================================

sample = X.iloc[[0]].values


# Sklearn
sklearn_prediction = model.predict(
    sample
)


# ONNX
onnx_result = session.run(
    None,
    {
        input_name: sample
    }
)


print("\n========================================")
print("PREDICTION")
print("========================================")

print(
    "Sklearn:",
    sklearn_prediction
)

print(
    "ONNX outputs:"
)

for i, result in enumerate(onnx_result):

    print(
        i,
        result
    )