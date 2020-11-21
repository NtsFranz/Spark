using System.Collections.Generic;

namespace IgniteBot2
{
	/// <summary>
	/// Data container interface. Parent for other data containers such as Events, Matches, Goals, Players...
	/// </summary>
	interface DataContainer
	{
		Dictionary<string, object> ToDict();

	}
}
