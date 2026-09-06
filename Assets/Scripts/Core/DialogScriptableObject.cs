using UnityEngine;
using System.Collections.Generic;
using FortressClash.UI;

namespace FortressClash.Core
{
    [System.Serializable]
    public struct DialogData
    {
        public string dialogName;
        public BaseDialog dialogPrefab;
    }

    [CreateAssetMenu(fileName = "DialogConfig", menuName = "UI/DialogConfig", order = 1)]
    public class DialogScriptableObject : ScriptableObject
    {
        public List<DialogData> dialogList;

        public BaseDialog GetDialogPrefab(string name)
        {
            foreach (var data in dialogList)
            {
                if (data.dialogName == name)
                    return data.dialogPrefab;
            }
            return null;
        }
    }
}