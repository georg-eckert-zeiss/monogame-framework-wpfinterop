using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Framework.WpfInterop;
using MonoGame.Framework.WpfInterop.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WpfTest.Components;

namespace WpfTest.Scenes
{
    /// <summary>
    /// Displays content that causes heavy load to simulate dropped frames.
    /// </summary>
    public class HeavyLoadScene : WpfGame
    {
        private BasicEffect _basicEffect;
        private WpfKeyboard _keyboard;
        private KeyboardState _keyboardState;
        private Matrix _projectionMatrix;
        private VertexBuffer _vertexBuffer;
        private VertexDeclaration _vertexDeclaration;
        private Matrix _viewMatrix;
        private Matrix _worldMatrix;
        private bool _disposed;

        private float _rotation;
        private RasterizerState _wireframe;
        private RasterizerState _filled;
        private TextComponent _loadMessage;
        private int _loadCount = 1;
        private readonly List<GameTime> _lastUpdates = new List<GameTime>();
        private readonly List<GameTime> _lastDraws = new List<GameTime>();
        private const int _lookbackPeriodInSeconds = 3;
        private const int _stepSize = 1;
        private const int _cubesPerRow = 10;

        protected override void Initialize()
        {
            _disposed = false;
            _ = new WpfGraphicsDeviceService(this)
            {
                PreferMultiSampling = true
            };
            Components.Add(new FpsComponent(this));
            Components.Add(new TimingComponent(this));
            var message = new TextComponent(this, "Use +/- to increase/decrease system load (will redraw cubes multiple times to simulate load)", new Vector2(1, 0), HorizontalAlignment.Right);
            Components.Add(message);

            _lastUpdates.Add(new GameTime(TimeSpan.Zero, TargetElapsedTime));
            _lastDraws.Add(new GameTime(TimeSpan.Zero, TargetElapsedTime));
            _loadMessage = new TextComponent(this, GetLoadMessage(), new Vector2(0, 0.1f), HorizontalAlignment.Left);
            Components.Add(_loadMessage);

            float tilt = MathHelper.ToRadians(0);  // 0 degree angle
                                                   // Use the world matrix to tilt the cube along x and y axes.
            _worldMatrix = Matrix.CreateRotationX(tilt) * Matrix.CreateRotationY(tilt);
            _viewMatrix = Matrix.CreateLookAt(new Vector3(25, 25, 25), Vector3.Zero, Vector3.Up);

            _basicEffect = new BasicEffect(GraphicsDevice);

            _basicEffect.World = _worldMatrix;
            _basicEffect.View = _viewMatrix;
            RefreshProjection();

            // primitive color
            _basicEffect.AmbientLightColor = new Vector3(0.1f, 0.1f, 0.1f);
            _basicEffect.DiffuseColor = new Vector3(1.0f, 1.0f, 1.0f);
            _basicEffect.SpecularColor = new Vector3(0.25f, 0.25f, 0.25f);
            _basicEffect.SpecularPower = 5.0f;
            _basicEffect.Alpha = 1.0f;

            // get a bunch of white cubes
            _basicEffect.LightingEnabled = false;

            _filled = new RasterizerState
            {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid
            };
            _wireframe = new RasterizerState
            {
                CullMode = CullMode.None,
                FillMode = FillMode.WireFrame
            };

            SetupCube();

            _keyboard = new WpfKeyboard(this);

            base.Initialize();
        }

        private string GetLoadMessage() => string.Join(Environment.NewLine,
            $"Cube count (simulated load): {_loadCount * _cubesPerRow * _cubesPerRow}",
            $"Last update duration: {_lastUpdates.Last().ElapsedGameTime.TotalSeconds:0.00}s",
            $"Average update count per second: {AveragePerSecond(_lastUpdates):0}",
            $"Last draw duration: {_lastDraws.Last().ElapsedGameTime.TotalSeconds:0.00}s",
            $"Average draw count per second: {AveragePerSecond(_lastDraws):0}");

        private double AveragePerSecond(List<GameTime> list)
        {
            if (list.Count == 0)
                return 0;

            var currentTime = list.Max(x => x.TotalGameTime);
            var minTime = currentTime.Add(TimeSpan.FromSeconds(-_lookbackPeriodInSeconds - 1));
            var dataset = list
                // get only the last 3 seconds, current second is not complete 999/1000 times so ignore it as well as it would only skew down the result
                .Where(x => x.TotalGameTime > minTime && x.TotalGameTime.Seconds < currentTime.Seconds)
                .ToArray();

            if (dataset.Length == 0)
                return 0;

            return dataset
                .GroupBy(x => x.TotalGameTime.Seconds)
                .Average(x => x.Count());
        }

        private void SetupCube()
        {
            _vertexDeclaration = new VertexDeclaration(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
                new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0)
            );

            Vector3 topLeftFront = new Vector3(-1.0f, 1.0f, 1.0f);
            Vector3 bottomLeftFront = new Vector3(-1.0f, -1.0f, 1.0f);
            Vector3 topRightFront = new Vector3(1.0f, 1.0f, 1.0f);
            Vector3 bottomRightFront = new Vector3(1.0f, -1.0f, 1.0f);
            Vector3 topLeftBack = new Vector3(-1.0f, 1.0f, -1.0f);
            Vector3 topRightBack = new Vector3(1.0f, 1.0f, -1.0f);
            Vector3 bottomLeftBack = new Vector3(-1.0f, -1.0f, -1.0f);
            Vector3 bottomRightBack = new Vector3(1.0f, -1.0f, -1.0f);

            Vector2 textureTopLeft = new Vector2(0.0f, 0.0f);
            Vector2 textureTopRight = new Vector2(1.0f, 0.0f);
            Vector2 textureBottomLeft = new Vector2(0.0f, 1.0f);
            Vector2 textureBottomRight = new Vector2(1.0f, 1.0f);

            Vector3 frontNormal = new Vector3(0.0f, 0.0f, 1.0f);
            Vector3 backNormal = new Vector3(0.0f, 0.0f, -1.0f);
            Vector3 topNormal = new Vector3(0.0f, 1.0f, 0.0f);
            Vector3 bottomNormal = new Vector3(0.0f, -1.0f, 0.0f);
            Vector3 leftNormal = new Vector3(-1.0f, 0.0f, 0.0f);
            Vector3 rightNormal = new Vector3(1.0f, 0.0f, 0.0f);

            var cubeVertices = new VertexPositionNormalTexture[36];

            // Front face.
            cubeVertices[0] = new VertexPositionNormalTexture(topLeftFront, frontNormal, textureTopLeft);
            cubeVertices[1] = new VertexPositionNormalTexture(bottomLeftFront, frontNormal, textureBottomLeft);
            cubeVertices[2] = new VertexPositionNormalTexture(topRightFront, frontNormal, textureTopRight);
            cubeVertices[3] = new VertexPositionNormalTexture(bottomLeftFront, frontNormal, textureBottomLeft);
            cubeVertices[4] = new VertexPositionNormalTexture(bottomRightFront, frontNormal, textureBottomRight);
            cubeVertices[5] = new VertexPositionNormalTexture(topRightFront, frontNormal, textureTopRight);

            // Back face.
            cubeVertices[6] = new VertexPositionNormalTexture(topLeftBack, backNormal, textureTopRight);
            cubeVertices[7] = new VertexPositionNormalTexture(topRightBack, backNormal, textureTopLeft);
            cubeVertices[8] = new VertexPositionNormalTexture(bottomLeftBack, backNormal, textureBottomRight);
            cubeVertices[9] = new VertexPositionNormalTexture(bottomLeftBack, backNormal, textureBottomRight);
            cubeVertices[10] = new VertexPositionNormalTexture(topRightBack, backNormal, textureTopLeft);
            cubeVertices[11] = new VertexPositionNormalTexture(bottomRightBack, backNormal, textureBottomLeft);

            // Top face.
            cubeVertices[12] = new VertexPositionNormalTexture(topLeftFront, topNormal, textureBottomLeft);
            cubeVertices[13] = new VertexPositionNormalTexture(topRightBack, topNormal, textureTopRight);
            cubeVertices[14] = new VertexPositionNormalTexture(topLeftBack, topNormal, textureTopLeft);
            cubeVertices[15] = new VertexPositionNormalTexture(topLeftFront, topNormal, textureBottomLeft);
            cubeVertices[16] = new VertexPositionNormalTexture(topRightFront, topNormal, textureBottomRight);
            cubeVertices[17] = new VertexPositionNormalTexture(topRightBack, topNormal, textureTopRight);

            // Bottom face.
            cubeVertices[18] = new VertexPositionNormalTexture(bottomLeftFront, bottomNormal, textureTopLeft);
            cubeVertices[19] = new VertexPositionNormalTexture(bottomLeftBack, bottomNormal, textureBottomLeft);
            cubeVertices[20] = new VertexPositionNormalTexture(bottomRightBack, bottomNormal, textureBottomRight);
            cubeVertices[21] = new VertexPositionNormalTexture(bottomLeftFront, bottomNormal, textureTopLeft);
            cubeVertices[22] = new VertexPositionNormalTexture(bottomRightBack, bottomNormal, textureBottomRight);
            cubeVertices[23] = new VertexPositionNormalTexture(bottomRightFront, bottomNormal, textureTopRight);

            // Left face.
            cubeVertices[24] = new VertexPositionNormalTexture(topLeftFront, leftNormal, textureTopRight);
            cubeVertices[25] = new VertexPositionNormalTexture(bottomLeftBack, leftNormal, textureBottomLeft);
            cubeVertices[26] = new VertexPositionNormalTexture(bottomLeftFront, leftNormal, textureBottomRight);
            cubeVertices[27] = new VertexPositionNormalTexture(topLeftBack, leftNormal, textureTopLeft);
            cubeVertices[28] = new VertexPositionNormalTexture(bottomLeftBack, leftNormal, textureBottomLeft);
            cubeVertices[29] = new VertexPositionNormalTexture(topLeftFront, leftNormal, textureTopRight);

            // Right face.
            cubeVertices[30] = new VertexPositionNormalTexture(topRightFront, rightNormal, textureTopLeft);
            cubeVertices[31] = new VertexPositionNormalTexture(bottomRightFront, rightNormal, textureBottomLeft);
            cubeVertices[32] = new VertexPositionNormalTexture(bottomRightBack, rightNormal, textureBottomRight);
            cubeVertices[33] = new VertexPositionNormalTexture(topRightBack, rightNormal, textureTopRight);
            cubeVertices[34] = new VertexPositionNormalTexture(topRightFront, rightNormal, textureTopLeft);
            cubeVertices[35] = new VertexPositionNormalTexture(bottomRightBack, rightNormal, textureBottomRight);

            _vertexBuffer = new VertexBuffer(GraphicsDevice, _vertexDeclaration, cubeVertices.Length, BufferUsage.None);
            _vertexBuffer.SetData(cubeVertices);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            Components.Clear();
            _disposed = true;

            _vertexBuffer.Dispose();
            _vertexBuffer = null;

            _vertexDeclaration.Dispose();
            _vertexDeclaration = null;

            _basicEffect.Dispose();
            _basicEffect = null;

            base.Dispose(disposing);
        }

        /// <summary>
        /// Update projection matrix values, both in the calculated matrix <see cref="_projectionMatrix"/> as well as
        /// the <see cref="_basicEffect"/> projection.
        /// </summary>
        private void RefreshProjection()
        {
            _projectionMatrix = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(45), // 45 degree angle
                (float)GraphicsDevice.Viewport.Width /
                (float)GraphicsDevice.Viewport.Height,
                1.0f, 100.0f);
            _basicEffect.Projection = _projectionMatrix;
        }

        protected override void Update(GameTime gameTime)
        {
            _lastUpdates.Add(gameTime);
            while (_lastUpdates.Count > 1.0 / TargetElapsedTime.TotalSeconds * _lookbackPeriodInSeconds)
            {
                // only keep records of last Xs
                _lastUpdates.RemoveAt(0);
            }

            var previousKeyboardState = _keyboardState;
            _keyboardState = _keyboard.GetState();

            bool IsKeyPressed(Keys k)
                => _keyboardState.IsKeyDown(k) && previousKeyboardState.IsKeyUp(k);

            if (IsKeyPressed(Keys.Add) || IsKeyPressed(Keys.OemPlus))
            {
                _loadCount += _stepSize;
            }
            if (IsKeyPressed(Keys.Subtract) || IsKeyPressed(Keys.OemMinus))
            {
                _loadCount = MathHelper.Clamp(_loadCount - _stepSize, 1, int.MaxValue);
            }
            _loadMessage.Text = GetLoadMessage();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime time)
        {
            _lastDraws.Add(time);
            while (_lastDraws.Count > 1.0 / TargetElapsedTime.TotalSeconds * _lookbackPeriodInSeconds)
            {
                // only keep records of last Xs
                _lastDraws.RemoveAt(0);
            }

            //The projection depends on viewport dimensions (aspect ratio).
            // Because WPF controls can be resized at any time (user resizes window)
            // we need to refresh the values each draw call, otherwise cube will look distorted to user
            RefreshProjection();

            GraphicsDevice.Clear(Color.CornflowerBlue);
            GraphicsDevice.DepthStencilState = new DepthStencilState
            {
                DepthBufferEnable = true
            };
            GraphicsDevice.SetVertexBuffer(_vertexBuffer);

            // Rotate cube around up-axis.
            // only update cube when the game is active
            if (IsActive)
                _rotation += (float)time.ElapsedGameTime.TotalMilliseconds / 10000 * MathHelper.TwoPi;

            for (int i = 0; i < _loadCount; i++)
            {
                DrawCubes(_filled, Color.White, i);
                DrawCubes(_wireframe, Color.Black, i);
            }

            base.Draw(time);
        }

        private void DrawCubes(RasterizerState rasterizerState, Color color, int yOffset)
        {
            GraphicsDevice.RasterizerState = rasterizerState;
            const float cubeSeperaration = 4;
            _basicEffect.DiffuseColor = color.ToVector3();

            var offset = new Vector3(cubeSeperaration * _cubesPerRow / 2, 0, cubeSeperaration * _cubesPerRow / 2);
            // really bad performance, could use instancing to improve performance a lot
            for (int y = 0; y < _cubesPerRow; y++)
            {
                for (int x = 0; x < _cubesPerRow; x++)
                {
                    _basicEffect.World = Matrix.CreateRotationY(_rotation) * _worldMatrix *
                                         Matrix.CreateTranslation(offset - new Vector3(x, yOffset, y) * cubeSeperaration);

                    foreach (var pass in _basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 12);
                    }
                }
            }
        }
    }
}
