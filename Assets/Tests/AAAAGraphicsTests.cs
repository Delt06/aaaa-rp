using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;

namespace Tests
{
    public class AAAAGraphicsTests
    {
        private const string ReferenceImagesPath = "Assets/Tests/ReferenceImages";

        [UnityTest] [Category("AAAA RP")]
        [PrebuildSetup("SetupGraphicsTestCases")]
        [UseGraphicsTestCases(ReferenceImagesPath)]
        public IEnumerator Run(GraphicsTestCase testCase)
        {
            SceneManager.LoadScene(testCase.ScenePath);

            // Always wait one frame for scene load
            yield return null;

            Camera[] cameras = GameObject.FindGameObjectsWithTag("MainCamera")
                .Select(x => x.GetComponent<Camera>())
                .Where(c => c != null)
                .ToArray();
            AAAAGraphicsTestSettings settings = Object.FindFirstObjectByType<AAAAGraphicsTestSettings>();
            Assert.IsNotNull(settings, "Invalid test scene, couldn't find OwlcatGraphicsTestSettings");

            yield return null;

            int waitFrames = settings.WaitFrames;

            if (settings.ImageComparisonSettings.UseBackBuffer && settings.WaitFrames < 1)
            {
                waitFrames = 1;
            }

            for (int i = 0; i < waitFrames; i++)
            {
                yield return new WaitForEndOfFrame();
            }

            ImageAssert.AreEqual(testCase.ReferenceImage, cameras.Where(x => x != null).OrderBy(x => x.depth), settings.ImageComparisonSettings);

            foreach (Camera camera in cameras)
            {
                // Does it allocate memory when it renders what's on the main camera?
                bool allocatesMemory = false;

                try
                {
                    ImageAssert.AllocatesMemory(camera, settings.ImageComparisonSettings);
                }
                catch (AssertionException)
                {
                    allocatesMemory = true;
                }

                if (allocatesMemory)
                {
                    Assert.Fail($"Allocated memory when rendering what is main camera {camera}", camera);
                }
            }


        }
    }
}