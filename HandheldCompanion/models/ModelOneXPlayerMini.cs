using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelOneXPlayerMini : Model
    {
        // Specific groups (move me)
        Model3DGroup ALetter;
        Model3DGroup ALetterInside;
        Model3DGroup BackGrillShadow;
        Model3DGroup BackIcon;
        Model3DGroup BLetter;
        Model3DGroup BLetterInside1;
        Model3DGroup BLetterInside2;
        Model3DGroup Screen;
        Model3DGroup Home;
        Model3DGroup LED;
        Model3DGroup LeftBaseRing;
        Model3DGroup RightBaseRing;
        Model3DGroup MKB;
        Model3DGroup MKBIcon;
        Model3DGroup SpeakerLeft;
        Model3DGroup SpeakerRight;
        Model3DGroup StartIcon;
        Model3DGroup TopGrill;
        Model3DGroup Turbo;
        Model3DGroup TurboIcon;
        Model3DGroup XLetter;
        Model3DGroup YLetter;

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
            ALetter = modelImporter.Load($"models/{ModelName}/ALetter.obj");
            ALetterInside = modelImporter.Load($"models/{ModelName}/ALetterInside.obj");
            BackGrillShadow = modelImporter.Load($"models/{ModelName}/BackGrillShadow.obj");
            BackIcon = modelImporter.Load($"models/{ModelName}/BackIcon.obj");
            BLetter = modelImporter.Load($"models/{ModelName}/BLetter.obj");
            BLetterInside1 = modelImporter.Load($"models/{ModelName}/BLetterInside1.obj");
            BLetterInside2 = modelImporter.Load($"models/{ModelName}/BLetterInside2.obj");
            Screen = modelImporter.Load($"models/{ModelName}/Screen.obj");
            Home = modelImporter.Load($"models/{ModelName}/Home.obj");
            LED = modelImporter.Load($"models/{ModelName}/LED.obj");
            LeftBaseRing = modelImporter.Load($"models/{ModelName}/LeftBaseRing.obj");
            RightBaseRing = modelImporter.Load($"models/{ModelName}/RightBaseRing.obj");
            MKB = modelImporter.Load($"models/{ModelName}/MKB.obj");
            MKBIcon = modelImporter.Load($"models/{ModelName}/MKBIcon.obj");
            SpeakerLeft = modelImporter.Load($"models/{ModelName}/SpeakerLeft.obj");
            SpeakerRight = modelImporter.Load($"models/{ModelName}/SpeakerRight.obj");
            StartIcon = modelImporter.Load($"models/{ModelName}/StartIcon.obj");
            TopGrill = modelImporter.Load($"models/{ModelName}/TopGrill.obj");
            Turbo = modelImporter.Load($"models/{ModelName}/Turbo.obj");
            TurboIcon = modelImporter.Load($"models/{ModelName}/TurboIcon.obj");
            XLetter = modelImporter.Load($"models/{ModelName}/XLetter.obj");
            YLetter = modelImporter.Load($"models/{ModelName}/YLetter.obj");
            // pull model(s)

            model3DGroup.Children.Add(ALetter);
            model3DGroup.Children.Add(ALetterInside);
            model3DGroup.Children.Add(BackGrillShadow);
            model3DGroup.Children.Add(BackIcon);
            model3DGroup.Children.Add(BLetter);
            model3DGroup.Children.Add(BLetterInside1);
            model3DGroup.Children.Add(BLetterInside2);
            model3DGroup.Children.Add(Screen);
            model3DGroup.Children.Add(Home);
            model3DGroup.Children.Add(LED);
            model3DGroup.Children.Add(LeftBaseRing);
            model3DGroup.Children.Add(RightBaseRing);
            model3DGroup.Children.Add(MKB);
            model3DGroup.Children.Add(MKBIcon);
            model3DGroup.Children.Add(SpeakerLeft);
            model3DGroup.Children.Add(SpeakerRight);
            model3DGroup.Children.Add(StartIcon);
            model3DGroup.Children.Add(TopGrill);
            model3DGroup.Children.Add(Turbo);
            model3DGroup.Children.Add(TurboIcon);
            model3DGroup.Children.Add(XLetter);
            model3DGroup.Children.Add(YLetter);

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                if (DefaultMaterials.ContainsKey(model3D))
                    continue;

                // generic material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                DefaultMaterials[model3D] = MaterialPlasticBlack;

                // specific material(s)
                if (model3D == MainBody || model3D == LeftMotor || model3D == RightMotor)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                    DefaultMaterials[model3D] = MaterialPlasticWhite;
                    continue;
                }

                // specific material(s)
                if (model3D == SpeakerLeft || model3D == SpeakerRight
                    || model3D == LeftBaseRing || model3D == RightBaseRing
                    || model3D == StartIcon || model3D == BackIcon
                    || model3D == Home
                    || model3D == ALetter || model3D == BLetter || model3D == XLetter || model3D == YLetter
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
