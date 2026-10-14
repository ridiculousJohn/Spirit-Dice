using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** Shared surface references keep encounter dice and modifier history trays visually consistent. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Spirit Tray")]
    public sealed class SpiritTrayPresentationAsset : ScriptableObject
    {
        public Shader DiceShader;
        public Texture2D BoneTexture;
        public Texture2D WoodTexture;
        /** Geometry and surface anchors are authored together in the encounter's table coordinate space. */
        public SpiritTrayGeometry GeometryPrefab;
        [HideInInspector] public Vector3 CameraPosition = new Vector3(0, 8, -6);
        public Vector3 CameraEulerAngles = new Vector3(53.13f, 0, 0);
        public bool Orthographic;
        public float FieldOfView = 25;
        [HideInInspector] public float OrthographicSize = 4;
        [HideInInspector] public float NearClipPlane = .1f;
        [HideInInspector] public float FarClipPlane = 30;
        /** The key light illuminates encounters without a sampled scene lighting profile. */
        public Vector3 FallbackLightEulerAngles = new Vector3(65, -32, 0);
        public Color FallbackLightColor = new Color(1, .94f, .8f, 1);
        public float FallbackLightIntensity = 1.2f;
        public Vector2Int TextureSize = new Vector2Int(1024, 512);
        /** The center and width place the tray; its displayed height follows the render texture aspect. */
        [HideInInspector] public Rect CompositionRect = new Rect(.02f, .19f, .96f, .32f);

        public Rect GetCompositionRect(float portraitAspect)
        {
            float height = CompositionRect.width * portraitAspect * TextureSize.y / TextureSize.x;
            var size = new Vector2(CompositionRect.width, height);
            return new Rect(CompositionRect.center - size * .5f, size);
        }

        public void ValidateScene()
        {
            Validate();
            if (GeometryPrefab == null) throw new InvalidOperationException("Spirit trays require authored geometry.");
            GeometryPrefab.Validate();
            if (TextureSize.x <= 0 || TextureSize.y <= 0 || FieldOfView <= 0 || FieldOfView >= 180)
                throw new InvalidOperationException("Spirit tray camera settings must describe a valid projection.");
            if (CompositionRect.width <= 0 || CompositionRect.height <= 0)
                throw new InvalidOperationException("Spirit tray placement requires a positive size.");
        }

        public void Validate()
        {
            if (DiceShader == null) throw new InvalidOperationException("Spirit trays require a dice surface shader.");
            if (BoneTexture == null) throw new InvalidOperationException("Spirit trays require a bone surface texture.");
            if (WoodTexture == null) throw new InvalidOperationException("Spirit trays require a wood surface texture.");
        }
    }
}
