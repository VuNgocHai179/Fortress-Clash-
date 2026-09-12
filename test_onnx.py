import numpy as np
import pandas as pd
import joblib
import onnxruntime as ort


# ============================================================
# CONFIG
# ============================================================

DATASET_PATH = "dataset.csv"

SCALER_PATH = "fortress_ai_scaler.pkl"

ONNX_MODEL = "fortress_ai_model.onnx"


# ============================================================
# ACTION MAPPING
# ============================================================

actions = [
    "WAITING",
    "CHOOSESWORD",
    "CHOOSEARCHER",
    "CHOOSESLINGER",
    "CHOOSESPEARMAN",
    "CHOOSESOLDIER",
    "CHOOSECATAPULT"
]


# ============================================================
# LOAD DATA
# ============================================================

df = pd.read_csv(DATASET_PATH)


print("========================================")
print("DATASET")
print("========================================")

print("Rows:", len(df))
print("Columns:", len(df.columns))


# ============================================================
# INPUT / OUTPUT
# ============================================================

X = df.drop(
    ["Action"],
    axis=1
)

y = df["Action"]


X = X.astype(
    np.float32
)


print("Input features:", X.shape[1])


if X.shape[1] != 19:

    raise ValueError(
        f"Expected 19 features, but got {X.shape[1]}"
    )


# ============================================================
# LOAD SCALER
# ============================================================

scaler = joblib.load(
    SCALER_PATH
)


print("\n========================================")
print("SCALER")
print("========================================")

print(
    "Scaler features:",
    scaler.n_features_in_
)


if scaler.n_features_in_ != 19:

    raise ValueError(
        f"Scaler expects {scaler.n_features_in_} "
        f"features instead of 19"
    )


# ============================================================
# LOAD ONNX
# ============================================================

session = ort.InferenceSession(

    ONNX_MODEL,

    providers=[
        "CPUExecutionProvider"
    ]

)


# ============================================================
# INPUT / OUTPUT INFO
# ============================================================

input_info = session.get_inputs()[0]

input_name = input_info.name

outputs = session.get_outputs()


print("\n========================================")
print("ONNX")
print("========================================")


print("Input:")

print(
    input_name,
    input_info.shape,
    input_info.type
)


print("\nOutputs:")

for output in outputs:

    print(
        output.name,
        output.shape,
        output.type
    )


# ============================================================
# CHECK ONNX INPUT
# ============================================================

onnx_input_shape = input_info.shape

if onnx_input_shape[-1] != 19:

    raise ValueError(
        f"ONNX expects {onnx_input_shape[-1]} "
        f"features instead of 19"
    )


# ============================================================
# TEST 1 SAMPLE
# ============================================================

sample_raw = X.iloc[
    [0]
].values


actual_action = y.iloc[0]


print("\n========================================")
print("TEST SAMPLE")
print("========================================")


print(
    "Actual:",
    actual_action
)


# ============================================================
# STANDARDIZE
# ============================================================

sample_scaled = scaler.transform(

    sample_raw

).astype(
    np.float32
)


print("\nRaw input:")

print(
    sample_raw
)


print("\nScaled input:")

print(
    sample_scaled
)


# ============================================================
# ONNX INFERENCE
# ============================================================

onnx_result = session.run(

    None,

    {
        input_name: sample_scaled
    }

)


# ============================================================
# GET LOGITS
# ============================================================

logits = onnx_result[0][0]


print("\nLogits shape:")

print(
    logits.shape
)


if len(logits) != 7:

    raise ValueError(
        f"Expected 7 outputs, but got {len(logits)}"
    )


# ============================================================
# PREDICT
# ============================================================

predicted_id = int(
    np.argmax(logits)
)


predicted_action = actions[
    predicted_id
]


# ============================================================
# RESULT
# ============================================================

print("\n========================================")
print("PREDICTION")
print("========================================")


print(
    "Actual:",
    actual_action
)


print(
    "Predicted ID:",
    predicted_id
)


print(
    "Predicted Action:",
    predicted_action
)


print(
    "\nLogits:"
)


for i, value in enumerate(logits):

    print(
        f"{i}: "
        f"{actions[i]:20s} "
        f"{value:.6f}"
    )