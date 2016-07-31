namespace R3.UI
{
	using System;
	using System.ComponentModel;
	using System.Drawing.Design;
	using System.Windows.Forms;
	using System.Windows.Forms.Design;

	public class TrackBarValueEditor<Type> : UITypeEditor
	{
		private IWindowsFormsEditorService editorService = null;

		public override object EditValue( ITypeDescriptorContext context, IServiceProvider provider, object value )
		{
			if( context == null || provider == null )
				return value;

			editorService = (IWindowsFormsEditorService)provider.GetService( typeof( IWindowsFormsEditorService ) );
			if( editorService == null )
				return value;

			// Create a new trackbar and set it up.
			TrackBar trackBar = new TrackBar();
			trackBar.ValueChanged += new EventHandler( this.ValueChanged );
			trackBar.MouseLeave += new EventHandler( this.MouseLeave );

			// Get the low/high values.
			PropertyDescriptor prop = context.PropertyDescriptor;
			RangeAttribute ra = prop.Attributes[typeof( RangeAttribute )] as RangeAttribute;
			double valueLow = ra.Low;
			double valueHigh = ra.High;

			if( typeof( Type ) == typeof( int ) )
			{
				trackBar.Minimum = (int)valueLow;
				trackBar.Maximum = (int)valueHigh;
				trackBar.TickFrequency = 1;
				trackBar.Value = Convert.ToInt32( value );
				editorService.DropDownControl( trackBar );
				value = trackBar.Value;
				return value;
			}

			trackBar.Minimum = 0;
			trackBar.Maximum = 100;
			trackBar.TickStyle = TickStyle.None;

			// Set the corresponding trackbar value.
			double percent = ( Convert.ToDouble( value ) - valueLow ) / ( valueHigh - valueLow );
			trackBar.Value = (int)( 100 * percent );

			// Show the control.
			editorService.DropDownControl( trackBar );

			// Here is the output value.
			value = valueLow + ( (double)trackBar.Value / 100 ) * ( valueHigh - valueLow );
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
