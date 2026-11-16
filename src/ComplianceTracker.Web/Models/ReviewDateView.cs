using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Web.Models
{
    // US-195 R1: for the views (@inject): the date a form fills in, as the date input wants it, and the hint under it
    public class ReviewDateView
    {
        private readonly ReviewDefaultsService _defaults;
        private ReviewDefaults? _known;

        public ReviewDateView(ReviewDefaultsService defaults)
        {
            _defaults = defaults;
        }

        public async Task<string> ValueAsync(ReviewDateKind kind)
            => (await _defaults.DateForAsync(kind)).ToString("yyyy-MM-dd");

        public async Task<string> HintAsync(ReviewDateKind kind)
        {
            _known ??= await _defaults.DefaultsAsync();
            return $"Default: {_known.MonthsFor(kind)} months (Settings). Leave it empty only if it never ends.";
        }
    }
}
