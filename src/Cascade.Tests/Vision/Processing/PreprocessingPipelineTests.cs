using System.Collections.Generic;
using Cascade.Tests.Vision;
using Cascade.Vision.Processing;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.Vision.Processing;

public class PreprocessingPipelineTests
{
    [Fact]
    public void ForScreenTextPipeline_TransformsImage()
    {
        var pipeline = PreprocessingPipeline.ForScreenText;
        var image = TestImageFactory.CreateSolidColor(System.Drawing.Color.LightGray);

        var processed = pipeline.Process(image);

        processed.Should().NotBeNullOrEmpty();
        processed.Should().NotEqual(image);
    }

    [Fact]
    public void CustomPipeline_RunsAllStepsInOrder()
    {
        var pipeline = new PreprocessingPipeline();
        var order = new List<int>();
        pipeline.AddCustom(data => { order.Add(1); return data; })
                .AddCustom(data => { order.Add(2); return data; });

        var image = TestImageFactory.CreateSolidColor(System.Drawing.Color.LightGray);
        pipeline.Process(image);

        order.Should().ContainInOrder(1, 2);
    }
}


