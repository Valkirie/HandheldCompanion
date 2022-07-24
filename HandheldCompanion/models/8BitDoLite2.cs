using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class Model8BitDoLite2 : Model
    {
        // Specific groups (move me)
        Model3DGroup BodyBack;
        Model3DGroup Logo;
        Model3DGroup Home;
        Model3DGroup LED1;
        Model3DGroup LED2;
        Model3DGroup LED3;
        Model3DGroup LED4;

        public Model8BitDoLite2() : base("8BitDoLite2")
        {
            // colors
            var ColorPlasticTurquoise = (Color)ColorConverter.ConvertFromString("#29C5CA");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#E2E2E2");
            var ColorHighlight = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];

            var MaterialPlasticTurquoise = new DiffuseMaterial(new SolidColorBrush(ColorPlasticTurquoise));
            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            var MaterialPlasticTransparentHighlight = new SpecularMaterial(ColorHighlight, 0.3);
            var MaterialHighlight = new DiffuseMaterial(ColorHighlight);

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-38.0f, -3.5f, 1.15f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(18.6f, -3.4f, -17.2f);
            JoystickMaxAngleDeg = 14.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-38.00f, -2.6f, 26.53f);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(38.00f, -2.6f, 26.53f);
            TriggerMaxAngleDeg = 16.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationPointLeft = new Vector3D(-38.00f, -2.6f, 26.53f);
            UpwardVisibilityRotationPointRight = new Vector3D(38.00f, -2.6f, 26.53f);

            // load model(s)
            BodyBack= modelImporter.Load($"models/{ModelName}/BodyBack.obj");
            Logo = modelImporter.Load($"models/{ModelName}/Logo.obj");
            Home = modelImporter.Load($"models/{ModelName}/Home.obj");
            LED1 = modelImporter.Load($"models/{ModelName}/LED1.obj");
            LED2 = modelImporter.Load($"models/{ModelName}/LED2.obj");
            LED3 = modelImporter.Load($"models/{ModelName}/LED3.obj");
            LED4 = modelImporter.Load($"models/{ModelName}/LED4.obj");

            // pull model(s)
            model3DGroup.Children.Add(BodyBack);
            model3DGroup.Children.Add(Logo);
            model3DGroup.Children.Add(Home);
            model3DGroup.Children.Add(LED1);
            model3DGroup.Children.Add(LED2);
            model3DGroup.Children.Add(LED3);
            model3DGroup.Children.Add(LED4);

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                if (DefaultMaterials.ContainsKey(model3D))
                    continue;

                // generic material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticWhite;
                DefaultMaterials[model3D] = MaterialPlasticWhite;

                // specific material(s)
                if (model3D == MainBody || model3D == BodyBack || model3D == LeftMotor || model3D == RightMotor)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticTurquoise;
                    DefaultMaterials[model3D] = MaterialPlasticTurquoise;
                    continue;
                }

                if (model3D == LED1)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticTransparentHighlight;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticTransparentHighlight;
                    DefaultMaterials[model3D] = MaterialPlasticTransparentHighlight;
                    continue;
                }

            }

            DrawHighligths();
        }
    }
}
