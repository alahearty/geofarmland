using FluentValidation;

namespace Geofarmland.Server.Features.Plots.UpdatePlot
{
    public class UpdatePlotValidator : AbstractValidator<UpdatePlotRequest>
    {
        public UpdatePlotValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Coordinates).Must(c => c != null && c.Length >= 3)
                .WithMessage("A polygon must have at least three points.");
        }
    }
}
