namespace MagicTile.Utils
{
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Helper class to read presentations of regular maps, from: 
	/// https://www.math.auckland.ac.nz/~conder/OrientableRegularMaps101.txt
	/// http://dfgm.math.msu.su/files/papers-sym/conder.pdf
	/// </summary>
	public class GroupPresentation
	{
		/// <summary>
		/// Read a presentation and return it as a list of reflection arrays.
		/// </summary>
		public static List<int[]> ReadRelations( string presentation )
		{
			// Remove the brackets.
			presentation = presentation.Trim( new char[] { '[', ']' } );

			// Get rid of whitespace.
			presentation = Regex.Replace( presentation, @"\s+", "" );

			List<int[]> result = new List<int[]>();
			string[] relationsCondensed = presentation.Split( ',' );
			foreach( string condensed in relationsCondensed )
			{
				// Fully supported formula parsing feels like overkill, since these 
				// relations are limited in their structure. We'll do the following:
				// (1) Expand powers of single items, which have no parenthesis.
				// (2) Expand powers of items in parenthesis.
				// (3) Replace R,S,T with a,b,c.
				// (4) Convert to integer mirrors.
				// (5) Remove repeats.

				string relation = ExpandSimplePowers( condensed );
				relation = ExpandParenPowersRecursive( relation );
				relation = RotaryToReflections( relation );
				int[] reflections = WordAsReflections( relation );
				int[] cleaned = reflections;
				//int[] cleaned = RemoveRepeatsRecursive( reflections );
				//( cleaned.Length > 0 )
					result.Add( cleaned );
			}
			
			return result;
		}

		private static char Start = '(';
		private static char End = ')';
		private static char Mult = '*';
		private static char Power = '^';

		private static string ExpandSimplePowers( string condensed )
		{
			Regex regex = new Regex( @"(?<!\))\^" );

			List<int> indices = new List<int>();
			foreach( Match match in regex.Matches( condensed ) )
				indices.Add( match.Index );

			if( indices.Count == 0 )
				return condensed;

			// Go backwards so we can alter the string without messing up indices.
			StringBuilder result = new StringBuilder( condensed );
			for( int i=indices.Count-1; i>=0; i-- )
			{
				int caret = indices[i];
				int power = ReadPower( condensed, caret );
				string expanded = ExpandPower( power, condensed.Substring( caret - 1, 1 ) );
				int length = power < 0 ? 4 : 3;
				result.Remove( caret - 1, length );
				result.Insert( caret - 1, expanded );
			}

			return result.ToString();
		}

		private static int ReadPower( string word, int caretIndex )
		{
			int start = caretIndex + 1;
			int length = 1;
			if( word[start] == '-' )
				length = 2;
			return int.Parse( word.Substring( start, length ) );
		}

		private static string ExpandPower( int power, string val )
		{
			if( power < 0 )
			{
				power *= -1;
				val = ReverseWord( val );
			}

			string result = "";
			for( int i = 0; i < power; i++ )
			{
				result += val;
				if( i != power - 1 )
					result += Mult;
			}
			return result;
		}

		private static string ExpandParenPowersRecursive( string condensed )
		{
			if( !condensed.Contains( Start ) )
				return condensed;

			int current = condensed.IndexOf( Start );
			int start = current;
			int count = 1;
			while( count > 0 )
			{
				current++;
				if( condensed[current] == Start )
					count++;
				if( condensed[current] == End )
					count--;
			}

			int end = current;

			Debug.Assert( condensed[end + 1] == Power );
			int power = ReadPower( condensed, end + 1 );

			string sub = condensed.Substring( start + 1, end - start - 1 );
			string result = condensed.Substring( 0, start );
			result += ExpandPower( power, sub );
			result += condensed.Substring( power < 0 ? end + 4 : end + 3 );
			result = result.TrimEnd( new char[] { ' ', Mult } );

			return ExpandParenPowersRecursive( result );
		}

		private static string ReverseWord( string word )
		{
			if( word.Contains( Power ) )
				throw new System.NotImplementedException();

			string[] split = word.Split( new char[] { Mult } );
			List<string> reversed = new List<string>();
			for( int i = split.Length - 1; i >= 0; i-- )
			{
				switch( split[i] )
				{
				case "R":
					reversed.Add( "r" );
					break;
				case "r":
					reversed.Add( "R" );
					break;
				case "S":
					reversed.Add( "s" );
					break;
				case "s":
					reversed.Add( "S" );
					break;
				case "T":
					reversed.Add( "T" );
					break;
				default:
					throw new System.ArgumentException();
				}
			}

			return string.Join( "*", reversed );
		}

		/// <summary>
		/// Turn a rotary based word (R,S,T) to a reflection based one (a,b,c)
		/// http://dfgm.math.msu.su/files/papers-sym/conder.pdf
		/// </summary>
		/// <param name=""></param>
		private static string RotaryToReflections( string rotaryWord )
		{
			rotaryWord = rotaryWord
				.Replace( "R", "a*b" )
				.Replace( "r", "b*a" )
				.Replace( "S", "b*c" )
				.Replace( "s", "c*b" )
				.Replace( "T", "b" );
			return rotaryWord;
		}

		/// <summary>
		/// The input word should be expanded.
		/// </summary>
		private static int[] WordAsReflections( string word )
		{
			List<int> result = new List<int>();
			string[] split = word.Trim().Split( Mult );
			foreach( string s in split )
			{
				switch( s )
				{
				case "a":
					result.Add( 0 );
					break;
				case "b":
					result.Add( 1 );
					break;
				case "c":
					result.Add( 2 );
					break;
				case "d":
					result.Add( 3 );
					break;
				}
			}
			return result.ToArray();
		}

		private static int[] RemoveRepeatsRecursive( int[] input )
		{
			List<int> cleaned = new List<int>();
			for( int i=0; i<input.Length; i++ )
			{
				if( i == input.Length - 1 || input[i] != input[i + 1] )
					cleaned.Add( input[i] );
				else
					i++;
			}

			if( cleaned.Count < input.Length )
				return RemoveRepeatsRecursive( cleaned.ToArray() );
			else
				return input;
		}
	}
}
