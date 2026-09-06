using System.Collections.Generic;
using UnityEngine;
using FortressClash.UI;

namespace FortressClash.Core
{
    public class UIManager : Singleton<UIManager>
    {
        [Header("Configuration")]
        public DialogScriptableObject dialogConfig;
        public Transform dialogParent;

        private Dictionary<string, BaseDialog> instantiatedDialogs = new Dictionary<string, BaseDialog>();
        private Stack<BaseDialog> dialogStack = new Stack<BaseDialog>();

        public BaseDialog ShowDialog(string dialogName)
        {
            BaseDialog dialogToShow = null;

            if (instantiatedDialogs.ContainsKey(dialogName))
            {
                dialogToShow = instantiatedDialogs[dialogName];
            }
            else
            {
                BaseDialog prefab = dialogConfig.GetDialogPrefab(dialogName);
                if (prefab != null)
                {
                    dialogToShow = Instantiate(prefab, dialogParent);
                    instantiatedDialogs.Add(dialogName, dialogToShow);
                }
            }

            if (dialogToShow != null)
            {
                dialogStack.Push(dialogToShow);
                dialogToShow.Show();

                dialogToShow.transform.SetAsLastSibling();
            }

            return dialogToShow;
        }

        public void PopDialog()
        {
            if (dialogStack.Count > 0)
            {
                BaseDialog topDialog = dialogStack.Pop();
                topDialog.Hide();
            }
            else
            {
                
            }
        }

        public void HideDialog(string dialogName)
        {
            if (instantiatedDialogs.ContainsKey(dialogName))
            {
                instantiatedDialogs[dialogName].Hide();
            }
        }

        public void HideAllDialogs()
        {
            while (dialogStack.Count > 0)
            {
                BaseDialog dialog = dialogStack.Pop();
                dialog.Hide();
            }
        }
    }
}