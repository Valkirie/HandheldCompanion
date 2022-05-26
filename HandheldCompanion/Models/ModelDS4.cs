using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelDS4 : Model
    {
        // Specific groups (move me)
        Model3DGroup LeftShoulderMiddle;
        Model3DGroup RightShoulderMiddle;
        Model3DGroup Screen;
        Model3DGroup MainBodyBack;
        Model3DGroup PlaystationButton;
        Model3DGroup AuxPort;
        Model3DGroup Triangle;

        public ModelDS4() : base("DS4")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#38383A");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#E0E0E0");
            var ColorHighlight = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];

            var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            var MaterialHighlight = new DiffuseMaterial(ColorHighlight);

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-25.5f, -5.086f, -21.582f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(25.5f, -5.086f, -21.582f);
            JoystickMaxAngleDeg = 19.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-38.061f, 3.09f, 26.842f);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(38.061f, 3.09f, 26.842f);
            TriggerMaxAngleDeg = 16.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationPointLeft = new Vector3D(-48.868f, -13f, 29.62f);
            UpwardVisibilityRotationPointRight = new Vector3D(48.868f, -13f, 29.62f);

            // load model(s)
            LeftShoulderMiddle = modelImporter.Load($"models/{ModelName}/Shoulder-Left-Middle.obj");
            RightShoulderMiddle = modelImporter.Load($"models/{ModelName}/Shoulder-Right-Middle.obj");
            Screen = modelImporter.Load($"models/{ModelName}/Screen.obj");
            MainBodyBack = modelImporter.Load($"models/{ModelName}/MainBodyBack.obj");
            PlaystationButton = modelImporter.Load($"models/{ModelName}/Playstation-Button.obj");
            AuxPort = modelImporter.Load($"models/{ModelName}/Aux-Port.obj");
            Triangle = modelImporter.Load($"models/{ModelName}/Triangle.obj");

            // pull model(s)
            model3DGroup.Children.Add(LeftShoulderMiddle);
            model3DGroup.Children.Add(RightShoulderMiddle);
            model3DGroup.Children.Add(Screen);
            model3DGroup.Children.Add(MainBodyBack);
            model3DGroup.Children.Add(PlaystationButton);
            model3DGroup.Children.Add(AuxPort);
            model3DGroup.Children.Add(Triangle);

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                // generic material(s)
                HighlightMaterials[model3D] = MaterialHighlight;
            }

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                if (DefaultMaterials.ContainsKey(model3D))
                    continue;

                // specific material(s)
                if (model3D == MainBody || model3D == LeftMotor || model3D == RightMotor || model3D == Triangle)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                    DefaultMaterials[model3D] = MaterialPlasticWhite;
                    continue;
                }

                // generic material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                DefaultMaterials[model3D] = MaterialPlasticBlack;
            }
        }
    }
}
