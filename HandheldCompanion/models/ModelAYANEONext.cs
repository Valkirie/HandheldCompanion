using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelAYANEONext : Model
    {
        // Specific groups (move me)
        Model3DGroup WFB1;
        Model3DGroup WFB2;
        Model3DGroup LeftShoulderMiddle;
        Model3DGroup RightShoulderMiddle;
        Model3DGroup Screen;
        Model3DGroup JoystickLeftCover;
        Model3DGroup JoystickRightCover;

        public ModelAYANEONext() : base("AYANEO Next")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#333333");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#F0EFF0");
            var ColorHighlight = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];

            var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            var MaterialHighlight = new DiffuseMaterial(ColorHighlight);

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-109.0f, -8.0f, 23.0f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(104.0f, -8.0f, -6.0f);
            JoystickMaxAngleDeg = 19.0f;
            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-105.951f, 1.25f, 46.814f);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(105.951f, 1.25f, 46.814f);
            TriggerMaxAngleDeg = 16.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationPointLeft = new Vector3D(-92.5f, -10.5f, 54.1f);
            UpwardVisibilityRotationPointRight = new Vector3D(92.5f, -10.5f, 54.1f);

            // load model(s)
            WFB1 = modelImporter.Load($"models/{ModelName}/WFB-1.obj");
            WFB2 = modelImporter.Load($"models/{ModelName}/WFB-2.obj");
            LeftShoulderMiddle = modelImporter.Load($"models/{ModelName}/Shoulder-Left-Middle.obj");
            RightShoulderMiddle = modelImporter.Load($"models/{ModelName}/Shoulder-Right-Middle.obj");
            Screen = modelImporter.Load($"models/{ModelName}/Screen.obj");
            JoystickLeftCover = modelImporter.Load($"models/{ModelName}/Joystick-Left-Cover.obj");
            JoystickRightCover = modelImporter.Load($"models/{ModelName}/Joystick-Right-Cover.obj");

            // pull model(s)
            model3DGroup.Children.Add(WFB1);
            model3DGroup.Children.Add(WFB2);
            model3DGroup.Children.Add(LeftShoulderMiddle);
            model3DGroup.Children.Add(RightShoulderMiddle);
            model3DGroup.Children.Add(JoystickLeftCover);
            model3DGroup.Children.Add(JoystickRightCover);
            model3DGroup.Children.Add(Screen);

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                // generic material(s)
                HighlightMaterials[model3D] = MaterialHighlight;
            }

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                // specific material(s)
                if (model3D == MainBody || model3D == LeftMotor || model3D == RightMotor)
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
