using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Spark
{
	/// <summary>
	/// Interaction logic for SliderWithLabel.xaml
	/// </summary>
	public partial class SliderWithLabel : UserControl
	{
		public SliderWithLabel()
		{
			InitializeComponent();
			// DataContext = this;
			
			Loaded += (sender, args) =>
			{
				SliderElement.SetBinding(ValueBindingProperty, new Binding("Value"));
			};
		}



		public static readonly DependencyProperty LabelProperty = DependencyProperty.Register("Label", typeof(string), typeof(SliderWithLabel));

		public string Label
		{
			get => GetValue(LabelProperty) as string;
			set => SetValue(LabelProperty, value);
		}
		
		

		public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(float), typeof(SliderWithLabel));
		public float Minimum
		{
			get => (float)GetValue(MinimumProperty);
			set => SetValue(MinimumProperty, value);
		}
		
		public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(float), typeof(SliderWithLabel));
		public float Maximum
		{
			get => (float)GetValue(MaximumProperty);
			set => SetValue(MaximumProperty, value);
		}

		public static readonly DependencyProperty ValueBindingProperty = DependencyProperty.Register("Value", typeof(string), typeof(SliderWithLabel), new UIPropertyMetadata());
		public string Value
		{
			get => GetValue(ValueBindingProperty) as string;
			set => SetValue(ValueBindingProperty, value);
		}
	}
}
