using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelMachenikeHG510 : Model
    {

        Model3DGroup BackBody;
        Model3DGroup FrontAccent;
        Model3DGroup FN;
        Model3DGroup Turbo;
        Model3DGroup HomeMachenike;
        Model3DGroup LED1;
        Model3DGroup LED2;
        Model3DGroup LED3;
        Model3DGroup LED4;
        Model3DGroup LED5;
        Model3DGroup LED6;
        Model3DGroup LED7;
        Model3DGroup LED8;

        public ModelMachenikeHG510() : base("MachenikeHG510")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#151515");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#C1C5C6");
            var ColorAccent = (Color)ColorConverter.ConvertFromString("#53544E");
            var ColorHighlight = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];

            var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            var MaterialAccent = new DiffuseMaterial(new SolidColorBrush(ColorAccent));
            var MaterialPlasticTransparentHighlight = new SpecularMaterial(ColorHighlight, 0.3);

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-39.0f, -1.2f, 16.5f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(20f, -1.2f, -7f);
            JoystickMaxAngleDeg = 14.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-31.87f, 2.67f, 41.38);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(31.87f, 2.67f, 41.38);
            TriggerMaxAngleDeg = 16.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationPointLeft = new Vector3D(-36.226f, -14.26f, 47.332f);
            UpwardVisibilityRotationPointRight = new Vector3D(36.226f, -14.26f, 47.332f);

            // load model(s)
            BackBody = modelImporter.Load($"models/{ModelName}/BackBody.obj");
            FrontAccent = modelImporter.Load($"models/{ModelName}/FrontAccent.obj");
            FN = modelImporter.Load($"models/{ModelName}/FN.obj");
            Turbo = modelImporter.Load($"models/{ModelName}/Turbo.obj");
            HomeMachenike = modelImporter.Load($"models/{ModelName}/Home-Machenike.obj");
            LED1 = modelImporter.Load($"models/{ModelName}/LED1.obj");
            LED2 = modelImporter.Load($"models/{ModelName}/LED2.obj");
            LED3 = modelImporter.Load($"models/{ModelName}/LED3.obj");
            LED4 = modelImporter.Load($"models/{ModelName}/LED4.obj");
            LED5 = modelImporter.Load($"models/{ModelName}/LED5.obj");
            LED6 = modelImporter.Load($"models/{ModelName}/LED6.obj");
            LED7 = modelImporter.Load($"models/{ModelName}/LED7.obj");
            LED8 = modelImporter.Load($"models/{ModelName}/LED8.obj");

            // pull model(s)
            model3DGroup.Children.Add(BackBody);
            model3DGroup.Children.Add(FrontAccent);
            model3DGroup.Children.Add(FN);
            model3DGroup.Children.Add(Turbo);
            model3DGroup.Children.Add(HomeMachenike);
            model3DGroup.Children.Add(LED1);
            model3DGroup.Children.Add(LED2);
            model3DGroup.Children.Add(LED3);
            model3DGroup.Children.Add(LED4);
            model3DGroup.Children.Add(LED5);
            model3DGroup.Children.Add(LED6);
            model3DGroup.Children.Add(LED7);
            model3DGroup.Children.Add(LED8);

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                if (DefaultMaterials.ContainsKey(model3D))
                    continue;

                // generic material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlack; // Model is not solid closed
                DefaultMaterials[model3D] = MaterialPlasticBlack;

                // specific material(s)
                if (model3D == MainBody || model3D == BackBody || model3D == LeftMotor || model3D == RightMotor)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                    DefaultMaterials[model3D] = MaterialPlasticWhite;
                    continue;
                }

                if (model3D == FrontAccent)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialAccent;
                    DefaultMaterials[model3D] = MaterialAccent;
                    continue;
                }

                if (model3D == LED1 || model3D == LED2 || model3D == LED3 || model3D == LED4 ||
                    model3D == LED5 || model3D == LED6 || model3D == LED7 || model3D == LED8)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticTransparentHighlight;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticTransparentHighlight;
                    DefaultMaterials[model3D] = MaterialPlasticTransparentHighlight;
                    continue;
                }

            }

            DrawHighligths();
        }

        private new void DrawHighligths()
        {
            var ColorHighlight = (Color)ColorConverter.ConvertFromString("#53544E");
            var MaterialHighlight = new DiffuseMaterial(new SolidColorBrush(ColorHighlight));

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                // generic material(s)
                HighlightMaterials[model3D] = MaterialHighlight;
            }
        }
    }
}
