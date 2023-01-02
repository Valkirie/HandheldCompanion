using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelOneXPlayerMini : Model
    {
        // Specific groups (move me)
        Model3DGroup B1Letter;
        Model3DGroup B2Letter;
        Model3DGroup B3Letter;
        Model3DGroup B4Letter;
        Model3DGroup BackGrillShadow;
        Model3DGroup BackIcon;
        Model3DGroup BodyInternal;
        Model3DGroup Screen;
        Model3DGroup LED;
        Model3DGroup LeftBaseRing;
        Model3DGroup RightBaseRing;
        Model3DGroup MKBIcon;
        Model3DGroup SpeakerLeft;
        Model3DGroup SpeakerRight;
        Model3DGroup StartIcon;
        Model3DGroup TopGrill;
        Model3DGroup TurboIcon;

        public ModelOneXPlayerMini() : base("OneXPlayerMini")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#111612");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#EBEBEB");
            var ColorPlasticOrange = (Color)ColorConverter.ConvertFromString("#DC633C");

            var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            var MaterialPlasticOrange = new DiffuseMaterial(new SolidColorBrush(ColorPlasticOrange));

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-108.5f, 9.0f, 21.0f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(103.0f, 9.0f, -9.0f);
            JoystickMaxAngleDeg = 10.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-105.951f, 1.25f, 46.814f);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(105.951f, 1.25f, 46.814f);
            TriggerMaxAngleDeg = 16.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(26.915, 0, 7.27);
            UpwardVisibilityRotationAxisRight = new Vector3D(26.915, 0, -7.27);
            UpwardVisibilityRotationPointLeft = new Vector3D(-93.32f, -10.5f, 54.05f);
            UpwardVisibilityRotationPointRight = new Vector3D(93.32, -10.5f, 54.05f);

            // load model(s)
            B1Letter = modelImporter.Load($"models/{ModelName}/B1Letter.obj");
            B2Letter = modelImporter.Load($"models/{ModelName}/B2Letter.obj");
            B3Letter = modelImporter.Load($"models/{ModelName}/B3Letter.obj");
            B4Letter = modelImporter.Load($"models/{ModelName}/B4Letter.obj");
            BackGrillShadow = modelImporter.Load($"models/{ModelName}/BackGrillShadow.obj");
            BackIcon = modelImporter.Load($"models/{ModelName}/BackIcon.obj");
            BodyInternal = modelImporter.Load($"models/{ModelName}/BodyInternal.obj");
            Screen = modelImporter.Load($"models/{ModelName}/Screen.obj");
            LED = modelImporter.Load($"models/{ModelName}/LED.obj");
            LeftBaseRing = modelImporter.Load($"models/{ModelName}/LeftBaseRing.obj");
            RightBaseRing = modelImporter.Load($"models/{ModelName}/RightBaseRing.obj");
            MKBIcon = modelImporter.Load($"models/{ModelName}/MKBIcon.obj");
            SpeakerLeft = modelImporter.Load($"models/{ModelName}/SpeakerLeft.obj");
            SpeakerRight = modelImporter.Load($"models/{ModelName}/SpeakerRight.obj");
            StartIcon = modelImporter.Load($"models/{ModelName}/StartIcon.obj");
            TopGrill = modelImporter.Load($"models/{ModelName}/TopGrill.obj");
            TurboIcon = modelImporter.Load($"models/{ModelName}/TurboIcon.obj");

            // pull model(s)
            model3DGroup.Children.Add(B1Letter);
            model3DGroup.Children.Add(B2Letter);
            model3DGroup.Children.Add(B3Letter);
            model3DGroup.Children.Add(B4Letter);
            model3DGroup.Children.Add(BackGrillShadow);
            model3DGroup.Children.Add(BackIcon);
            model3DGroup.Children.Add(BodyInternal);
            model3DGroup.Children.Add(Screen);
            model3DGroup.Children.Add(LED);
            model3DGroup.Children.Add(LeftBaseRing);
            model3DGroup.Children.Add(RightBaseRing);
            model3DGroup.Children.Add(MKBIcon);
            model3DGroup.Children.Add(SpeakerLeft);
            model3DGroup.Children.Add(SpeakerRight);
            model3DGroup.Children.Add(StartIcon);
            model3DGroup.Children.Add(TopGrill);
            model3DGroup.Children.Add(TurboIcon);

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                if (DefaultMaterials.ContainsKey(model3D))
                    continue;

                // generic material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                DefaultMaterials[model3D] = MaterialPlasticBlack;

                // specific material(s)
                if (model3D.Equals(MainBody)
                    || model3D.Equals(LeftMotor) || model3D.Equals(RightMotor)
                    || model3D.Equals(TurboIcon) || model3D.Equals(MKBIcon))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                    DefaultMaterials[model3D] = MaterialPlasticWhite;
                    continue;
                }

                // specific material(s)
                if (model3D.Equals(SpeakerLeft) || model3D.Equals(SpeakerRight)
                    || model3D.Equals(LeftBaseRing) || model3D.Equals(RightBaseRing)
                    || model3D.Equals(StartIcon) || model3D.Equals(BackIcon)
                    || model3D.Equals(B1Letter) || model3D.Equals(B2Letter) || model3D.Equals(B3Letter) || model3D.Equals(B4Letter)
                    )
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticOrange;
                    DefaultMaterials[model3D] = MaterialPlasticOrange;
                    continue;
                }

            }

            DrawHighligths();
        }
        private new void DrawHighligths()
        {
            var ColorHighlight = (Color)ColorConverter.ConvertFromString("#DC633C");
            var MaterialHighlight = new DiffuseMaterial(new SolidColorBrush(ColorHighlight));

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                // generic material(s)
                HighlightMaterials[model3D] = MaterialHighlight;
            }
        }
    }
}
