using UnityEngine;

namespace DELTation.AAAARP
{
    internal static class ShaderUtils
    {
#if UNITY_EDITOR
        private static float _mostRecentValidDeltaTime;
#endif
        
        public static float PersistentDeltaTime
        {
            get
            {
#if UNITY_EDITOR
                float deltaTime = Time.deltaTime;
                
                // The only case I'm aware of when a deltaTime of 0 is valid is when Time.timeScale is 0
                if (deltaTime > 0.0f || Time.timeScale == 0.0f)
                {
                    _mostRecentValidDeltaTime = deltaTime;
                }
                return _mostRecentValidDeltaTime;
#else
                return Time.deltaTime;
#endif
            }
        }
    }
}