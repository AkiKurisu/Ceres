using System;
using R3;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Ceres.Localization
{
    /// <summary>
    /// Binds an element property such as text or tooltip to a localization key
    /// </summary>
    [UxmlObject]
    public partial class LocalizedTextBinding : CustomBinding
    {
        private IDisposable _changed;

        [UxmlAttribute]
        public string key { get; set; }

        public LocalizedTextBinding()
        {
            updateTrigger = BindingUpdateTrigger.WhenDirty;
        }

        protected override void OnActivated(in BindingActivationContext context)
        {
            _changed = LocalizationSystem.Changed.Subscribe(_ => MarkDirty());
            MarkDirty();
        }

        protected override void OnDeactivated(in BindingActivationContext context)
        {
            _changed?.Dispose();
            _changed = null;
        }

        protected override BindingResult Update(in BindingContext context)
        {
            var element = context.targetElement;
            var path = new PropertyPath(context.bindingId);
            return ConverterGroups.TrySetValueGlobal(ref element, path, LocalizationSystem.Get(key), out VisitReturnCode code)
                ? new BindingResult(BindingStatus.Success)
                : new BindingResult(BindingStatus.Failure, $"[Ceres] Cannot set '{context.bindingId}' for key '{key}': {code}.");
        }
    }
}
