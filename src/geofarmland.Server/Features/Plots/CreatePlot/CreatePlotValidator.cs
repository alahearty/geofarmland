using FluentValidation;

namespace Geofarmland.Server.Features.Plots.CreatePlot
{
    public class CreatePlotValidator : AbstractValidator<CreatePlotRequest>
    {
        public CreatePlotValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Coordinates).NotEmpty().WithMessage("At least three coordinates are required.");
            RuleFor(x => x.Coordinates).Must(c => c != null && c.Length >= 3)
                .WithMessage("A polygon must have at least three points.");
        }
    }
}
