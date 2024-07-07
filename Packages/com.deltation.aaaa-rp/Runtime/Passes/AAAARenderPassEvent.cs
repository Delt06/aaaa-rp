namespace DELTation.AAAARP.Passes
{
    public enum AAAARenderPassEvent
    {
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> before rendering any other passes in the pipeline.
        ///     Camera matrices and stereo rendering are not setup this point.
        ///     You can use this to draw to custom input textures used later in the pipeline, f.ex LUT textures.
        /// </summary>
        BeforeRendering = 0,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> before rendering shadowmaps.
        ///     Camera matrices and stereo rendering are not setup this point.
        /// </summary>
        BeforeRenderingShadows = 50,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> after rendering shadowmaps.
        ///     Camera matrices and stereo rendering are not setup this point.
        /// </summary>
        AfterRenderingShadows = 100,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> before rendering prepasses, f.ex, depth prepass.
        ///     Camera matrices and stereo rendering are already setup at this point.
        /// </summary>
        BeforeRenderingPrePasses = 150,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> after rendering prepasses, f.ex, depth prepass.
        ///     Camera matrices and stereo rendering are already setup at this point.
        /// </summary>
        AfterRenderingPrePasses = 200,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> before rendering gbuffer pass.
        /// </summary>
        BeforeRenderingGbuffer = 210,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> after rendering gbuffer pass.
        /// </summary>
        AfterRenderingGbuffer = 220,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> before rendering deferred shading pass.
        /// </summary>
        BeforeRenderingDeferredLights = 230,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> after rendering deferred shading pass.
        /// </summary>
        AfterRenderingDeferredLights = 240,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> before rendering opaque objects.
        /// </summary>
        BeforeRenderingOpaques = 250,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> after rendering opaque objects.
        /// </summary>
        AfterRenderingOpaques = 300,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> before rendering the sky.
        /// </summary>
        BeforeRenderingSkybox = 350,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> after rendering the sky.
        /// </summary>
        AfterRenderingSkybox = 400,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> before rendering transparent objects.
        /// </summary>
        BeforeRenderingTransparents = 450,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> after rendering transparent objects.
        /// </summary>
        AfterRenderingTransparents = 500,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> before rendering post-processing effects.
        /// </summary>
        BeforeRenderingPostProcessing = 550,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> after rendering post-processing effects but before final blit,
        ///     post-processing AA effects and color grading.
        /// </summary>
        AfterRenderingPostProcessing = 600,
        
        /// <summary>
        ///     Executes a <c>ScriptableRenderPass</c> after rendering all effects.
        /// </summary>
        AfterRendering = 1000,
    }
}