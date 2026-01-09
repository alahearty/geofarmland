using FluentValidation;

namespace Geofarmland.Server.Features.Plots.CreatePlot
{
    public class CreatePlotValidator : AbstractValidator<CreatePlotRequest>
    {
        public CreatePlotValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Coordinates).NotEmpty();
        }
    }
}
