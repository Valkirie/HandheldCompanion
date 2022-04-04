using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class DS4 : HandheldModels
    {
        // Specific groups (move me)
        Model3DGroup LeftShoulderMiddle;
        Model3DGroup RightShoulderMiddle;
        Model3DGroup Screen;
        Model3DGroup MainBodyBack;
        Model3DGroup PlaystationButton;
        Model3DGroup AuxPort;
        Model3DGroup Triangle;

        public DS4() : base("DS4")
        {
            this.ModelLocked = true;

            // colors
            ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#38383A");
            ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#E0E0E0");
            ColorHighlight = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];

            MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            MaterialHighlight = new DiffuseMaterial(ColorHighlight);

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-25.5f, -5.086f, -21.582f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(25.5f, -5.086f, -21.582f);
            JoystickMaxAngleDeg = 19.0f;
            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-38.061f, 3.09f, 26.842f);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(38.061f, 3.09f, 26.842f);
            TriggerMaxAngleDeg = 16.0f;

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
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;

            // specific color(s)
            ((GeometryModel3D)MainBody.Children[0]).Material = MaterialPlasticWhite;
            ((GeometryModel3D)Triangle.Children[0]).Material = MaterialPlasticWhite;
        }
    }
}
