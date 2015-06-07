namespace R3.UI
{
	using System;
	using System.ComponentModel;
	using System.Drawing.Design;
	using System.Windows.Forms;
	using System.Windows.Forms.Design;

	public class TrackBarValueEditor : UITypeEditor
	{
		private IWindowsFormsEditorService editorService = null;
		double valueLow;
		double valueHigh;

		public override object EditValue( ITypeDescriptorContext context, IServiceProvider provider, object value )
		{
			if( context != null && provider != null )
			{
				editorService = (IWindowsFormsEditorService)provider.GetService( typeof( IWindowsFormsEditorService ) );
				if( editorService != null )
				{
					// Create a new trackbar and set it up.
					TrackBar trackBar = new TrackBar();
					trackBar.ValueChanged += new EventHandler( this.ValueChanged );
					trackBar.MouseLeave += new EventHandler( this.MouseLeave );
					trackBar.Minimum = 0;
					trackBar.Maximum = 100;
					trackBar.TickStyle = TickStyle.None;

					// Get the low/high values.
					PropertyDescriptor prop = context.PropertyDescriptor;
					RangeAttribute ra = prop.Attributes[typeof( RangeAttribute )] as RangeAttribute;
					valueLow = ra.Low;
					valueHigh = ra.High;

					// Set the corresponding trackbar value.
					double percent = ( System.Convert.ToDouble( value ) - valueLow ) / ( valueHigh - valueLow );
					trackBar.Value = (int)( 100 * percent );

					// Show the control.
					editorService.DropDownControl( trackBar );

					// Here is the output value.
					value = valueLow + ( (double)trackBar.Value / 100 ) * ( valueHigh - valueLow );
				}
			}

			return value;
		}

		public override UITypeEditorEditStyle GetEditStyle( ITypeDescriptorContext context )
		{
			if( context != null && context.Instance != null )
				return UITypeEditorEditStyle.DropDown;

			return base.GetEditStyle( context );
		}

		private void ValueChanged( object sender, EventArgs e )
		{
			// I couldn't figure out how to update the text here, but that would be nice.
		}

		private void MouseLeave( object sender, System.EventArgs e )
		{
			if( editorService != null )
				editorService.CloseDropDown();
		}
	}
}
