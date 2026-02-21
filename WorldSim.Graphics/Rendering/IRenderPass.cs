namespace WorldSim.Graphics.Rendering;

public interface IRenderPass
{
    string Name { get; }

    void Draw(in RenderFrameContext context);
}
