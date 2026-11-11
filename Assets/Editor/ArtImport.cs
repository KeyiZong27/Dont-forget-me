using System.IO;
using UnityEditor;
using UnityEngine;

namespace DontForgetMe.Editor
{
    // Import rules for everything under Assets/Art, so the settings survive a fresh clone
    // even before the .meta files have been generated.
    public sealed class ArtImport : AssetPostprocessor
    {
        public const string Root = "Assets/Art/";

        public override uint GetVersion()
        {
            return 4;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            string name = Path.GetFileName(assetPath).ToLowerInvariant();
            if (assetPath.StartsWith(Root + "Sky/"))
            {
                importer.textureShape = TextureImporterShape.TextureCube;
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
                settings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
                settings.seamlessCubemap = true;
                importer.SetTextureSettings(settings);
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                return;
            }

            bool normal = name.Contains("_nor_gl_") || name.Contains("_normal") || name.Contains("_bump");
            bool data = normal || name.Contains("_mask_") || name.Contains("_rough_") || name.Contains("_metal_");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !data;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 2048;
            importer.alphaIsTransparency = name.Contains("opacity");
            importer.textureCompression = TextureImporterCompression.Compressed;
            bool tiles = assetPath.StartsWith(Root + "Surfaces/");
            importer.wrapMode = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            if (assetPath.StartsWith(Root + "Animations/"))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
            }
            else if (assetPath.StartsWith(Root + "Characters/Dog/"))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = false;
            }
            else if (assetPath.StartsWith(Root + "Characters/"))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root + "Animations/")) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            string file = Path.GetFileNameWithoutExtension(assetPath);
            bool travelling = file.Contains("_walk_") || file.Contains("_run_");
            bool once = file.Contains("try_door");
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = file;
                clip.loopTime = !once;
                clip.loopPose = !once;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                // Travel stays as root motion, which the motors discard so they keep control of position.
                clip.lockRootPositionXZ = !travelling;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
