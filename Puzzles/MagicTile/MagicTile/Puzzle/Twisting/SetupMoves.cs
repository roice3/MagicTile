namespace MagicTile
{
	using System.Collections.Generic;
	using System.Linq;

	public class SetupMoves
	{
		public void Reset()
		{
			m_setupMoves.Reset();
			m_commutatorMoves.Reset();
			m_recordingSetup = m_recordingCommutator = false;
		}

		public void StartRecording()
		{
			m_setupMoves.StartRecording();
			m_recordingSetup = true;
		}

		public void Update( SingleTwist twist )
		{
			if( m_recordingSetup )
				m_setupMoves.Update( twist );

			if( m_recordingCommutator )
				m_commutatorMoves.Update( twist );
		}

		public void StopRecording()
		{
			m_setupMoves.StopRecording();
			m_recordingSetup = false;

			m_commutatorMoves.StartRecording();
			m_recordingCommutator = true;
		}

		public bool RecordingSetup { get { return m_recordingSetup; } }
		public bool RecordingCommutator { get { return m_recordingCommutator; } }

		public SingleTwist[] UnwindTwists
		{
			get 
			{
				SingleTwist[] result = m_setupMoves.ReverseTwists;

				// Stop the recording of any moves.
				Reset();

				return result; 
			}
		}

		public SingleTwist[] CommutatorTwists
		{
			get
			{
				// NOTE:
				// For ABA'B', they have already done AB at this point.
				// So we are only returning the second half of the commutator.
				List<SingleTwist> result = m_setupMoves.ReverseTwists.ToList();
				result.AddRange( m_commutatorMoves.ReverseTwists );
				
				// Stop the recording of any moves.
				Reset();

				return result.ToArray();
			}
		}

		private bool m_recordingSetup;
		private bool m_recordingCommutator;
		private readonly Macro m_setupMoves = new Macro();
		private readonly Macro m_commutatorMoves = new Macro();
	}
}
