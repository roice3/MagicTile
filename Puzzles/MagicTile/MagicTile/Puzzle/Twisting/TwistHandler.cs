namespace MagicTile
{
	using OpenTK;
	using R3.Drawing;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using Math = System.Math;

	internal class TwistHandler
	{
		public TwistHandler( GLControl glControl, System.Action status, Settings settings, RenderToTexture renderToTexture )
		{
			m_glControl = glControl;
			m_status = status;
			m_settings = settings;
			m_renderToTexture = renderToTexture;

			m_timer = new System.Timers.Timer( 30 );
			m_timer.SynchronizingObject = glControl;
			m_timer.Enabled = false;
			m_timer.Elapsed += new System.Timers.ElapsedEventHandler( TimerTick );

			m_setupMoves = new SetupMoves();
			m_workingMacro = new Macro();

			m_rotation = 0;
			this.Solving = false;
		}

		private System.Timers.Timer m_timer;
		private TwistHistory m_twistHistory;
		private SingleTwist m_currentTwist;

		// Macro stuff.
		public SetupMoves m_setupMoves { get; set; }
		public Macro m_workingMacro { get; set; }

		// References we need.
		private GLControl m_glControl;
		private System.Action m_status;
		private Puzzle m_puzzle;
		private Settings m_settings;
		private RenderToTexture m_renderToTexture;

		public void PuzzleUpdated( Puzzle puzzle )
		{
			m_puzzle = puzzle;
			m_twistHistory = m_puzzle.TwistHistory;
			m_setupMoves.Reset();
			m_workingMacro.Reset();
			m_currentTwist = null;
		}

		/// <summary>
		/// Returns the smoothed current rotation.
		/// </summary>
		public double SmoothedRotation
		{
			get
			{
				if( m_currentTwist == null )
					return 0;

				double max = m_currentTwist.Magnitude;
				return ( max / 2.0 ) * ( -Math.Cos( Math.PI * m_rotation / max ) + 1 );
			}
		}

		/// <summary>
		/// Access to the current twist.
		/// </summary>
		public SingleTwist CurrrentTwist
		{
			get { return m_currentTwist; }
		}

		/// <summary>
		/// The amount of rotation we've performed in a twist.
		/// </summary>
		private double m_rotation;

		/// <summary>
		/// Whether or not we are twisting.
		/// </summary>
		public bool Twisting
		{
			get { return m_timer.Enabled; }
		}

		public void Undo()
		{
			if( this.Twisting )
				return;

			UndoInternal();
		}

		private bool UndoInternal()
		{
			SingleTwist undo;
			bool couldUndo = m_twistHistory.GetUndoTwist( out undo );
			if( couldUndo )
			{
				if( undo.MacroEnd )
					ApplyUndoBlockInstantaneously( undo );
				else
					StartRotate( undo );
			}
			return couldUndo;
		}

		public void Redo()
		{
			if( this.Twisting )
				return;

			SingleTwist redo;
			if( m_twistHistory.GetRedoTwist( out redo ) )
			{
				if( redo.MacroStart )
					ApplyRedoBlockInstantaneously( redo );
				else
					StartRotate( redo );
			}
		}

		public void Solve() 
		{
			if( this.Twisting )
				return;

			// Start the first undo if we can, and mark us as solving.
			Solving = UndoInternal();
		}

		public bool Solving { get; set; }

		public void StartRotate( SingleTwist twist )
		{
			if( this.Twisting )
				return;

			m_rotation = 0;
			m_currentTwist = twist;
			m_currentTwist.IdentifiedTwistData.StartTwist( m_currentTwist.SliceMask, m_puzzle.IsSpherical );
			m_timer.Enabled = true;
		}

		private void IterateRotate()
		{
			if( m_currentTwist == null )
				return;

			foreach( Cell master in m_currentTwist.IdentifiedTwistData.AffectedMasterCells )
				m_renderToTexture.InvalidateTexture( master );
			if( m_puzzle.HasSurfaceConfig )
			{
				m_renderToTexture.InvalidateTexture( PuzzleRenderer.SurfaceTexture1 );
				m_renderToTexture.InvalidateTexture( PuzzleRenderer.SurfaceTexture2 );
			}

			m_rotation += R3.Core.Utils.DegreesToRadians( m_settings.RotationStep( m_currentTwist.IdentifiedTwistData.Order ) );
			//Trace.WriteLine( "rotation " + m_rotation );
			if( m_rotation > m_currentTwist.Magnitude )
			{
				m_rotation = m_currentTwist.Magnitude;
				FinishRotate();

				// Special handling if we are solving.
				if( this.Solving )
				{
					// Start the next undo, if we can.
					if( !UndoInternal() )
					{
						this.Solving = false;

						// Do this so auto-solves won't beep when complete.
						m_twistHistory.Scrambled = false;
					}
				}
			}

			m_glControl.Invalidate();
		}

		private void FinishRotate( bool updateStatus = true )
		{
			//Trace.WriteLine( "FinishRotate " );
			m_timer.Enabled = false;
			m_rotation = 0;

			// Update the state.
			m_puzzle.UpdateState( m_currentTwist );
			BeepIfSolved();

			// Track the history.
			// NOTE: This needs to happen after the 'Undoing' check in BeepIfSolved method above.
			m_twistHistory.Update( m_currentTwist );
			m_setupMoves.Update( m_currentTwist );
			m_workingMacro.Update( m_currentTwist );

			m_currentTwist.IdentifiedTwistData.EndTwist( m_currentTwist.SliceMask, m_puzzle.IsSpherical );
			m_currentTwist = null;

			if( updateStatus )
				m_status();
		}

		private void BeepIfSolved()
		{
			// ZZZ - can beep too much.
			if( !m_twistHistory.Undoing && m_twistHistory.Scrambled && m_puzzle.State.IsSolved )
				System.Media.SystemSounds.Asterisk.Play();
		}

		public void Unwind()
		{
			ApplyMacroTwistsInstantaneously( m_setupMoves.UnwindTwists );
		}

		public void Commutator()
		{
			ApplyMacroTwistsInstantaneously( m_setupMoves.CommutatorTwists );
		}

		public void ApplyMacro( Macro m, bool reverse )
		{
			if( reverse )
				ApplyMacroTwistsInstantaneously( m.ReverseTwists );
			else
				ApplyMacroTwistsInstantaneously( m.Twists );
		}

		/// <summary>
		/// NOTE: This is not meant to be called when undoing/redoing.
		/// </summary>
		private void ApplyMacroTwistsInstantaneously( SingleTwist[] twists )
		{
			for( int i=0; i<twists.Length; i++ )
			{
				m_currentTwist = twists[i];

				// Don't mark as macro if there is only one twist in the macro.
				if( twists.Length > 1 )
				{
					if( i == 0 )
						m_currentTwist.MacroStart = true;
					if( i == twists.Length - 1 )
						m_currentTwist.MacroEnd = true;
				}

				FinishRotate( updateStatus: false );
			}

			// ZZZ - could be smarter and invalidate less.
			InvalidateAllAndUpdateStatus();
		}

		/// <summary>
		/// Grabs a set of undo twists we want to apply all at once.
		/// </summary>
		private void ApplyUndoBlockInstantaneously( SingleTwist start )
		{
			m_currentTwist = start;
			FinishRotate( updateStatus: false );

			if( !start.MacroStart )
			{
				SingleTwist undo;
				while( m_twistHistory.GetUndoTwist( out undo ) )
				{
					m_currentTwist = undo;
					FinishRotate( updateStatus: false );
					if( undo.MacroStart )
						break;
				}
			}

			// ZZZ - could be smarter and invalidate less.
			InvalidateAllAndUpdateStatus();

			// Bump the solve to continue.
			if( this.Solving )
				Solve();
		}

		/// <summary>
		/// Grabs a set of redo twists we want to apply all at once.
		/// </summary>
		private void ApplyRedoBlockInstantaneously( SingleTwist start )
		{
			m_currentTwist = start;
			FinishRotate( updateStatus: false );

			if( !start.MacroEnd )
			{
				SingleTwist redo;
				while( m_twistHistory.GetRedoTwist( out redo ) )
				{
					m_currentTwist = redo;
					FinishRotate( updateStatus: false );
					if( redo.MacroEnd )
						break;
				}
			}

			// ZZZ - could be smarter and invalidate less.
			InvalidateAllAndUpdateStatus();
		}

		private void TimerTick( object source, System.Timers.ElapsedEventArgs e )
		{
			count++;
			//Trace.WriteLine( "timer " + count );
			IterateRotate();			
		}
		static int count = 0;

		/// <summary>
		/// Scramble.
		/// </summary>
		/// <param name="numTwists"></param>
		public void Scramble( int numTwists )
		{
			System.Random rand = new System.Random();
			List<IdentifiedTwistData> allTwistData = m_puzzle.AllTwistData;
			if( allTwistData.Count == 0 )
				return;

			for( int i = 0; i < numTwists; i++ )
			{
				m_currentTwist = new SingleTwist();
				m_currentTwist.IdentifiedTwistData = allTwistData[rand.Next( allTwistData.Count )];
				m_currentTwist.LeftClick = rand.Next( 2 ) == 1;

				// Try to avoid repeats of last (suggested by Melinda).
				IdentifiedTwistData last = m_twistHistory.AllTwists.Count == 0 ? null : m_twistHistory.AllTwists.Last().IdentifiedTwistData;
				if( last != null && allTwistData.Count > 2 )
				{
					while( last == m_currentTwist.IdentifiedTwistData )
						m_currentTwist.IdentifiedTwistData = allTwistData[rand.Next( allTwistData.Count )];
				}
				else
					m_currentTwist.IdentifiedTwistData = allTwistData[rand.Next( allTwistData.Count )];

				int numSlices = m_currentTwist.IdentifiedTwistData.TwistDataForStateCalcs.First().NumSlices;
				int randomSlice = rand.Next( numSlices ) + 1;
				m_currentTwist.SliceMask = SliceMask.SliceToMask( randomSlice );

				// Apply the twist.
				FinishRotate( updateStatus: false );
			}

			m_twistHistory.Scrambles += numTwists;

			InvalidateAllAndUpdateStatus();
		}

		public void ResetState()
		{
			m_puzzle.State.Reset();
			m_twistHistory.Clear();
			m_setupMoves.Reset();
			m_workingMacro.Reset();

			InvalidateAllAndUpdateStatus();
		}

		private void InvalidateAllAndUpdateStatus()
		{
			m_renderToTexture.InvalidateAllTextures();
			m_status();
			m_glControl.Invalidate();
		}
	}
}
