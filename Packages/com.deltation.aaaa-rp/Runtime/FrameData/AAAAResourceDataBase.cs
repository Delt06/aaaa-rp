using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public abstract class AAAAResourceDataBase : ContextItem
    {
        internal bool IsAccessible { get; set; }

        internal void BeginFrame()
        {
            IsAccessible = true;
        }

        internal void EndFrame()
        {
            IsAccessible = false;
        }

        /// <summary>
        ///     Updates the texture handle if the texture is accessible.
        /// </summary>
        /// <param name="handle">Handle to update.</param>
        /// <param name="newHandle">Handle of the new data.</param>
        protected void CheckAndSetTextureHandle(ref TextureHandle handle, TextureHandle newHandle)
        {
            if (!CheckAndWarnAboutAccessibility())
                return;

            handle = newHandle;
        }

        /// <summary>
        ///     Fetches the texture handle if the texture is accessible.
        /// </summary>
        /// <param name="handle">Handle to the texture you want to retrieve</param>
        /// <returns>Returns the handle if the texture is accessible and a null handle otherwise.</returns>
        protected TextureHandle CheckAndGetTextureHandle(ref TextureHandle handle)
        {
            if (!CheckAndWarnAboutAccessibility())
                return TextureHandle.nullHandle;

            return handle;
        }

        protected void CheckAndSetBufferHandle(ref BufferHandle handle, BufferHandle newHandle)
        {
            if (!CheckAndWarnAboutAccessibility())
            {
                return;
            }

            handle = newHandle;
        }

        protected BufferHandle CheckAndGetBufferHandle(ref BufferHandle handle)
        {
            if (!CheckAndWarnAboutAccessibility())
            {
                return BufferHandle.nullHandle;
            }

            return handle;
        }

        /// <summary>
        ///     Updates the texture handles if the texture is accessible. The current and new handles needs to be of the same size.
        /// </summary>
        /// <param name="handle">Handles to update.</param>
        /// <param name="newHandle">Handles of the new data.</param>
        protected void CheckAndSetTextureHandle(ref TextureHandle[] handle, TextureHandle[] newHandle)
        {
            if (!CheckAndWarnAboutAccessibility())
            {
                return;
            }

            if (handle == null || handle.Length != newHandle.Length)
            {
                handle = new TextureHandle[newHandle.Length];
            }

            for (int i = 0; i < newHandle.Length; i++)
            {
                handle[i] = newHandle[i];
            }
        }

        /// <summary>
        ///     Fetches the texture handles if the texture is accessible.
        /// </summary>
        /// <param name="handle">Handles to the texture you want to retrieve</param>
        /// <returns>Returns the handles if the texture is accessible and a null handle otherwise.</returns>
        protected TextureHandle[] CheckAndGetTextureHandle(ref TextureHandle[] handle)
        {
            return !CheckAndWarnAboutAccessibility() ? new[] { TextureHandle.nullHandle } : handle;

        }

        /// <summary>
        ///     Check if the texture is accessible.
        /// </summary>
        /// <returns>Returns true if the texture is accessible and false otherwise.</returns>
        protected bool CheckAndWarnAboutAccessibility()
        {
            if (!IsAccessible)
            {
                Debug.LogError("Trying to access resources outside of the current frame setup.");
            }

            return IsAccessible;
        }

        internal enum ActiveID
        {
            /// <summary>The camera buffer.</summary>
            Camera,

            /// <summary>The backbuffer.</summary>
            BackBuffer,
        }
    }

}