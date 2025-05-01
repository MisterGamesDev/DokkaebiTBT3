using Dokkaebi.Core.Data;
using Dokkaebi.Common;
using Dokkaebi.Interfaces;

namespace Dokkaebi.Units
{
    /// <summary>
    /// Represents an instance of a status effect applied to a unit.
    /// </summary>
    public class StatusEffectInstance : IStatusEffectInstance
    {
        private StatusEffectData _effectData;
        private int _remainingDuration;
        private int _sourceUnitId;
        private int _linkedUnitId = -1;
        public int linkedUnitId
        {
            get => _linkedUnitId;
            set => _linkedUnitId = value;
        }
        
        public StatusEffectInstance(StatusEffectData effect, int duration, int sourceUnit)
        {
            this._effectData = effect;
            this._remainingDuration = duration >= 0 ? duration : effect.duration;
            this._sourceUnitId = sourceUnit;
            this.linkedUnitId = -1;
        }

        // Core properties
        public StatusEffectData Effect => _effectData;
        public int RemainingDuration { get => _remainingDuration; set => _remainingDuration = value; }
        
        // IStatusEffectInstance implementation
        public StatusEffectType StatusEffectType => _effectData?.effectType ?? StatusEffectType.None;
        public int Duration => _effectData?.duration ?? 0;
        public int RemainingTurns => _remainingDuration;
        public int SourceUnitId => _sourceUnitId;
        
        // Legacy properties for UI compatibility
        public StatusEffectType effectType => StatusEffectType;
        public StatusEffectData effectData => Effect;
        public bool isPermanent => Effect?.isPermanent ?? false;
        public int remainingDuration => RemainingDuration;
        public int stacks { get; set; } = 1;
    }
} 