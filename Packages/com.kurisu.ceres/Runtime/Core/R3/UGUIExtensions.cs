using UnityEngine.UI;

namespace R3.Ceres
{
    public static class ReactivePropertyExtensions
    {
        /// <summary>
        /// Bind <see cref="ReactiveProperty{Single}"/> to slider
        /// </summary>
        /// <param name="slider"></param>
        /// <param name="property"></param>
        /// <param name="unRegister"></param>
        public static void BindProperty(this Slider slider, ReactiveProperty<float> property, IDisposableUnregister unRegister)
        {
            slider.onValueChanged.AsObservable().Subscribe(e => property.Value = e).AddTo(unRegister);
            property.Subscribe(slider.SetValueWithoutNotify).AddTo(unRegister);
            slider.SetValueWithoutNotify(property.Value);
        }
        
        /// <summary>
        /// Bind <see cref="ReactiveProperty{Int32}"/> to slider
        /// </summary>
        /// <param name="slider"></param>
        /// <param name="property"></param>
        /// <param name="unRegister"></param>
        public static void BindProperty(this Slider slider, ReactiveProperty<int> property, IDisposableUnregister unRegister)
        {
            slider.onValueChanged.AsObservable().Subscribe(e => property.Value = (int)e).AddTo(unRegister);
            property.Subscribe(e => slider.SetValueWithoutNotify(e)).AddTo(unRegister);
            slider.SetValueWithoutNotify(property.Value);
        }
        
        /// <summary>
        /// Bind <see cref="ReactiveProperty{Boolean}"/> to toggle
        /// </summary>
        /// <param name="toggle"></param>
        /// <param name="property"></param>
        /// <param name="unRegister"></param>
        public static void BindProperty(this Toggle toggle, ReactiveProperty<bool> property, IDisposableUnregister unRegister)
        {
            toggle.onValueChanged.AsObservable().Subscribe(e => property.Value = e).AddTo(unRegister);
            property.Subscribe(toggle.SetIsOnWithoutNotify).AddTo(unRegister);
            toggle.SetIsOnWithoutNotify(property.Value);
        }
    }
}