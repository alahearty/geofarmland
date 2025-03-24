using FluentValidation;

namespace geofarmland.Server.Application.Features.Plots.CreatePlot
{
    public class CreatePlotValidator : AbstractValidator<CreatePlotRequest>
    {
        public CreatePlotValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.GeoJson).NotEmpty();
        }
    }
}
