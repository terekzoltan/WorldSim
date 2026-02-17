using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Assets;
using WorldSim.Graphics.Camera;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class WorldRenderer
{
    private readonly TerrainRenderPass _terrainPass = new();
    private readonly ResourceRenderPass _resourcePass = new();
    private readonly StructureRenderPass _structurePass = new();
    private readonly ActorRenderPass _actorPass = new();

    public WorldRenderSettings Settings { get; }
    public WorldRenderTheme Theme { get; }

    public WorldRenderer(WorldRenderSettings? settings = null, WorldRenderTheme? theme = null)
    {
        Settings = settings ?? new WorldRenderSettings();
        Theme = theme ?? WorldRenderTheme.Default;
    }

    public void Draw(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, Camera2D camera, TextureCatalog textures)
    {
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.BuildMatrix());

        _terrainPass.Draw(spriteBatch, snapshot, textures, Settings, Theme);
        _resourcePass.Draw(spriteBatch, snapshot, textures, Settings, Theme);
        _structurePass.Draw(spriteBatch, snapshot, textures, Settings);
        _actorPass.Draw(spriteBatch, snapshot, textures, Settings, Theme);

        spriteBatch.End();
    }
}
