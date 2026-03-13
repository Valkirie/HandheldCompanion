using HandheldCompanion.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.Selectors
{
    public class LibraryItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? WideTemplate { get; set; }
        public DataTemplate? GridTemplate { get; set; }
        public DataTemplate? ListTemplate { get; set; }

        public bool IsGridView { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is ProfileViewModel profile)
            {
                // Use wide template for favorites
                if (profile.IsLiked && WideTemplate != null)
                    return WideTemplate;

                // Use grid or list template based on view mode
                return IsGridView ? GridTemplate : ListTemplate;
            }

            return base.SelectTemplate(item, container);
        }
    }
}
