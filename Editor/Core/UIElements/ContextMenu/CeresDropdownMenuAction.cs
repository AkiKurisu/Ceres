using System;
using UnityEngine.UIElements;
namespace Ceres.Editor
{
    public class CeresDropdownMenuAction : DropdownMenuAction
    {
        public CeresDropdownMenuAction(
            string actionName,
            Action<DropdownMenuAction> actionCallback,
            Func<DropdownMenuAction, Status> actionStatusCallback,
            object userData = null
        ) : base(actionName, actionCallback, actionStatusCallback, userData)
        {
        }

        public CeresDropdownMenuAction(
            string actionName,
            Action<DropdownMenuAction> actionCallback
        ) : this(actionName, actionCallback, (e) => Status.Normal, null)
        {
        }
    }
}