import pandas as pd
import torch
import torch.nn as nn

from torch.utils.data import TensorDataset, DataLoader

from sklearn.model_selection import train_test_split
from sklearn.preprocessing import StandardScaler
from sklearn.metrics import accuracy_score, classification_report

import joblib


# ============================================================
# CONFIG
# ============================================================

DATASET_PATH = "dataset.csv"

MODEL_PATH = "fortress_ai_model.pth"

SCALER_PATH = "fortress_ai_scaler.pkl"

BATCH_SIZE = 16

EPOCHS = 1000

LEARNING_RATE = 0.001


# ============================================================
# 1. LOAD DATASET
# ============================================================

df = pd.read_csv(DATASET_PATH)

print("========================================")
print("DATASET")
print("========================================")

print("Rows:", len(df))
print("Columns:", len(df.columns))

print(df.head())


# ============================================================
# 2. CHECK DATASET
# ============================================================

print("\n========================================")
print("DATASET INFO")
print("========================================")

print(df.dtypes)

print("\nMissing values:")

print(df.isnull().sum())


# ============================================================
# 3. INPUT / OUTPUT
# ============================================================

X = df.drop(
    ["Action"],
    axis=1
)

y = df["Action"]


print("\n========================================")
print("FEATURES")
print("========================================")

print("Input features:", X.shape[1])

for i, column in enumerate(X.columns):

    print(
        i,
        "->",
        column
    )


# ============================================================
# 4. FIXED ACTION MAPPING
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


action_to_id = {

    action: i

    for i, action in enumerate(actions)

}


id_to_action = {

    i: action

    for action, i in action_to_id.items()

}


print("\n========================================")
print("ACTION MAPPING")
print("========================================")

for i in range(len(actions)):

    print(
        i,
        "->",
        id_to_action[i]
    )


# ============================================================
# CHECK UNKNOWN ACTION
# ============================================================

unknown_actions = set(y.unique()) - set(actions)

if unknown_actions:

    raise ValueError(
        f"Unknown actions found: {unknown_actions}"
    )


# ============================================================
# ENCODE ACTION
# ============================================================

y = y.map(
    action_to_id
)


# ============================================================
# 5. CONVERT DATA TYPE
# ============================================================

X = X.astype("float32")

y = y.astype("int64")


# ============================================================
# 6. TRAIN / TEST SPLIT
# ============================================================

X_train, X_test, y_train, y_test = train_test_split(

    X,

    y,

    test_size=0.1,

    random_state=42,

    stratify=y

)


print("\n========================================")
print("SPLIT")
print("========================================")

print(
    "Train:",
    len(X_train)
)

print(
    "Test:",
    len(X_test)
)


# ============================================================
# 7. STANDARDIZE
# ============================================================

scaler = StandardScaler()


X_train = scaler.fit_transform(
    X_train
)


X_test = scaler.transform(
    X_test
)


# ============================================================
# SAVE SCALER
# ============================================================

joblib.dump(
    scaler,
    SCALER_PATH
)


print("\nScaler saved:", SCALER_PATH)


# ============================================================
# 8. PYTORCH TENSORS
# ============================================================

X_train_tensor = torch.tensor(

    X_train,

    dtype=torch.float32

)


y_train_tensor = torch.tensor(

    y_train.values,

    dtype=torch.long

)


X_test_tensor = torch.tensor(

    X_test,

    dtype=torch.float32

)


y_test_tensor = torch.tensor(

    y_test.values,

    dtype=torch.long

)


# ============================================================
# 9. DATA LOADER
# ============================================================

train_dataset = TensorDataset(

    X_train_tensor,

    y_train_tensor

)


train_loader = DataLoader(

    train_dataset,

    batch_size=BATCH_SIZE,

    shuffle=True

)


# ============================================================
# 10. MODEL
# ============================================================

class FortressAI(nn.Module):

    def __init__(

        self,

        input_size,

        output_size

    ):

        super().__init__()


        self.network = nn.Sequential(

            nn.Linear(
                input_size,
                64
            ),

            nn.ReLU(),


            nn.Linear(
                64,
                32
            ),

            nn.ReLU(),


            nn.Linear(
                32,
                output_size
            )

        )


    def forward(self, x):

        return self.network(x)


# ============================================================
# 11. CREATE MODEL
# ============================================================

input_size = X_train.shape[1]

output_size = len(actions)


print("\n========================================")
print("MODEL")
print("========================================")

print(
    "Input:",
    input_size
)

print(
    "Output:",
    output_size
)


model = FortressAI(

    input_size,

    output_size

)


print(model)


# ============================================================
# 12. LOSS
# ============================================================

criterion = nn.CrossEntropyLoss()


optimizer = torch.optim.Adam(

    model.parameters(),

    lr=LEARNING_RATE

)


# ============================================================
# 13. TRAIN
# ============================================================

print("\n========================================")
print("TRAINING")
print("========================================")


for epoch in range(EPOCHS):

    model.train()

    total_loss = 0


    for batch_X, batch_y in train_loader:

        # Forward

        outputs = model(
            batch_X
        )


        # Loss

        loss = criterion(

            outputs,

            batch_y

        )


        # Clear gradient

        optimizer.zero_grad()


        # Backpropagation

        loss.backward()


        # Update weights

        optimizer.step()


        total_loss += loss.item()


    if (epoch + 1) % 10 == 0:

        average_loss = (

            total_loss

            /

            len(train_loader)

        )


        print(

            f"Epoch "

            f"{epoch + 1:3d}/{EPOCHS}"

            f" - Loss: "

            f"{average_loss:.6f}"

        )


# ============================================================
# 14. EVALUATE
# ============================================================

model.eval()


with torch.no_grad():

    outputs = model(

        X_test_tensor

    )


    predictions = torch.argmax(

        outputs,

        dim=1

    )


predictions = predictions.numpy()


accuracy = accuracy_score(

    y_test,

    predictions

)


print("\n========================================")
print("RESULT")
print("========================================")


print(

    f"Accuracy: "

    f"{accuracy:.4f}"

)


print("\nClassification Report:")


print(

    classification_report(

        y_test,

        predictions,

        labels=list(range(len(actions))),

        target_names=actions,

        zero_division=0

    )

)


# ============================================================
# 15. SAVE MODEL
# ============================================================

torch.save(

    model.state_dict(),

    MODEL_PATH

)


print("\n========================================")
print("SAVE")
print("========================================")


print(

    "PyTorch model:",

    MODEL_PATH

)


print(

    "Scaler:",

    SCALER_PATH

)