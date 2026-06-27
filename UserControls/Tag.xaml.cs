using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;


namespace AnimeGirlsDownloader.UserControls
{
    public sealed partial class Tag : UserControl
    {
        public string TagText
        {
            get => (string)GetValue(TagTextProperty);
            set => SetValue(TagTextProperty, value);
        }
        public static readonly DependencyProperty TagTextProperty = DependencyProperty
            .Register(nameof(TagText), typeof(string), typeof(Tag), new PropertyMetadata(string.Empty));


        public Tag()
        {
            InitializeComponent();
        }
    }
}
