using SurgicalDetector.Models;
using System.Windows;
using System.Windows.Controls;
namespace SurgicalDetector.ViewModels
{
    public class MessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate UserTemplate { get; set; }
        public DataTemplate ModelTemplate { get; set; }
    public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ChatMessage message)
            {
                return message.IsUser ? UserTemplate : ModelTemplate;
            }
            return base.SelectTemplate(item, container);
        }
    }
}