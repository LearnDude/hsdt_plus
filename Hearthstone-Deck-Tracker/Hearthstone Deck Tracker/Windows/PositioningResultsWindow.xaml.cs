using Hearthstone_Deck_Tracker.BobsBuddy;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Positioning;

namespace Hearthstone_Deck_Tracker.Windows
{
	public partial class PositioningResultsWindow
	{
		public PositioningResultsWindow(PositioningResult result)
		{
			DataContext = new PositioningResultsViewModel(result);
			InitializeComponent();
			Title = $"Positioning Results — Turn {result.Turn}";
		}
	}
}
