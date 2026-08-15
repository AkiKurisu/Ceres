using Ceres.Flow.Annotations;
namespace Ceres.Gameplay
{
    /// <summary>
    /// A player controller is an <see cref="Actor"/> responsible for controller another actor
    /// </summary>
    public class PlayerController : Actor
    {
        private Actor _actor;
        
        [ExecutableFunction]
        public virtual bool IsBot()
        {
            return false;
        }
        
        [ExecutableFunction]
        public void SetActor(Actor actor)
        {
            if (_actor != null)
            {
                _actor.UnbindController(this);
                _actor = null;
            }
            _actor = actor;
            if (actor)
            {
                actor.BindController(this);
            }
        }
        
        [ImplementableEvent]
        protected override void OnDestroy()
        {
            if (_actor) _actor.UnbindController(this);
            _actor = null;
            base.OnDestroy();
        }
        
        public TActor GetTActor<TActor>() where TActor : Actor
        {
            return _actor as TActor;
        }
        
        [ExecutableFunction]
        public Actor GetActor()
        {
            return _actor;
        }
    }
}
