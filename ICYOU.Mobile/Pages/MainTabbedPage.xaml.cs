namespace ICYOU.Mobile.Pages;

public partial class MainTabbedPage : TabbedPage
{
    public MainTabbedPage()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainTabbedPage] Starting initialization...");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[MainTabbedPage] Initialization completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainTabbedPage] ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[MainTabbedPage] Stack: {ex.StackTrace}");
            throw;
        }
    }
}
