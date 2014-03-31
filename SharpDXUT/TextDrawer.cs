using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.D3DCompiler;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using Resource = SharpDX.Direct3D11.Resource;
using System.Runtime.InteropServices;

namespace SharpDXUT
{
    class TextDrawer : IDisposable
    {
        private Texture2D texture;
        private ShaderResourceView textureView;
        private VertexShader vertexShader;
        private PixelShader pixelShader;
        private SamplerState sampler;
        private InputLayout layout;
        private BlendState blendState;
        private Device device;
        private DeviceContext context;
        private int backBufferWidth;
        private int backBufferHeight;

        List<SpriteVertex> fontVertices = new List<SpriteVertex>();
        Buffer fontBuffer11;
        int fontBufferBytes11 = 0;

        public Color ForegroundColor;
        public Point InsertionPos;
        public int LineHeight;

        [StructLayout(LayoutKind.Sequential)]
        private struct SpriteVertex
        {
            public Vector3 Pos;
            public Vector4 Dif;
            public Vector2 Tex;
        }

        public TextDrawer(Device device, DeviceContext context)
        {
            this.device = device;
            this.context = context;
            this.ForegroundColor = new Color(1f, 1f, 1f, 1f);
            this.InsertionPos = new Point(0, 0);
            this.LineHeight = 15;
        }

        public void Load()
        {
            // Compile Vertex and Pixel shaders
            var vertexShaderByteCode = ShaderBytecode.CompileFromFile("Font.fx", "VS", "vs_4_0", ShaderFlags.None, EffectFlags.None);
            var vertexShader = new VertexShader(device, vertexShaderByteCode);
            this.vertexShader = vertexShader;

            var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Font.fx", "PS", "ps_4_0", ShaderFlags.None, EffectFlags.None);
            var pixelShader = new PixelShader(device, pixelShaderByteCode);
            this.pixelShader = pixelShader;
            // Layout from VertexShader input signature
            layout = new InputLayout(device, ShaderSignature.GetInputSignature(vertexShaderByteCode), new[]
                    {
                        new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                        new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0),
                        new InputElement("TEXCOORD", 0, Format.R32G32_Float, 28, 0)
                    });

            // Load texture and create sampler
            texture = Texture2D.FromFile<Texture2D>(device, "Font.dds");
            textureView = new ShaderResourceView(device, texture);

            sampler = new SamplerState(device, new SamplerStateDescription()
            {
                Filter = Filter.Anisotropic,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                ComparisonFunction = Comparison.Never,
                MaximumAnisotropy = 16,
                MinimumLod = 0,
                MaximumLod = float.MaxValue,
            });

            // Instantiate Vertex buiffer from vertex data
            var blendStateDesc = new BlendStateDescription()
            {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false,
            };
            for(int i=0; i<8; i++)
            {
                blendStateDesc.RenderTarget[i].IsBlendEnabled = true;
                blendStateDesc.RenderTarget[i].SourceBlend = BlendOption.SourceAlpha;
                blendStateDesc.RenderTarget[i].DestinationBlend = BlendOption.InverseSourceAlpha;
                blendStateDesc.RenderTarget[i].BlendOperation = BlendOperation.Add;
                blendStateDesc.RenderTarget[i].SourceAlphaBlend = BlendOption.One;
                blendStateDesc.RenderTarget[i].DestinationAlphaBlend = BlendOption.Zero;
                blendStateDesc.RenderTarget[i].AlphaBlendOperation = BlendOperation.Add;
                blendStateDesc.RenderTarget[i].RenderTargetWriteMask = ColorWriteMaskFlags.All;
            }
            blendState = new BlendState(device, blendStateDesc);
        }

        public void DrawFormattedTextLine(string format, params object[] args)
        {
            DrawTextLine(string.Format(format, args));
        }

        public void DrawTextLine(string text)
        {
            var rec = new RectangleF(InsertionPos.X, InsertionPos.Y, 0, 0);
            DrawText11(device, context, text, rec, ForegroundColor, backBufferWidth, backBufferHeight, false);
            InsertionPos.Y += LineHeight;  
        }

        public void DrawFormattedTextLine(RectangleF rc, string format, params object[] args)
        {
            DrawTextLine(rc, string.Format(format, args));
        }

        public void DrawTextLine(RectangleF rc, string text)
        {
            DrawText11(device, context, text, rc, ForegroundColor, backBufferWidth, backBufferHeight, false);
            InsertionPos.Y += LineHeight;
        }

        public void Unload()
        {
            textureView.Dispose();
            texture.Dispose();
            vertexShader.Dispose();
            pixelShader.Dispose();
        }

        private DepthStencilState depthStancilStateStored;
        private RasterizerState rasterizerStateStored;
        private BlendState blendStateStored;
        private SamplerState samplerStateStored;

        public void Begin(int width, int height)
        {
            this.backBufferWidth = width;
            this.backBufferHeight = height;

            // Store states
            depthStancilStateStored = context.OutputMerger.DepthStencilState;
            rasterizerStateStored = context.Rasterizer.State;
            blendStateStored = context.OutputMerger.BlendState;
            samplerStateStored = context.PixelShader.GetSamplers(0, 1)[0];

            // Apply shaders and stages
            context.InputAssembler.InputLayout = layout;
            context.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(fontBuffer11, Utilities.SizeOf<SpriteVertex>(), 0));
            context.VertexShader.Set(vertexShader);
            context.PixelShader.Set(pixelShader);
            context.PixelShader.SetSampler(0, sampler);
            context.PixelShader.SetShaderResource(0, textureView);
            context.OutputMerger.BlendState = blendState;

            InsertionPos.X = 0;
            InsertionPos.Y = 0;
        }

        public void End(DeviceContext context)
        {
            // Restore states
            context.OutputMerger.DepthStencilState = depthStancilStateStored;
            context.Rasterizer.State = rasterizerStateStored;
            context.OutputMerger.BlendState = blendStateStored;
            context.PixelShader.SetSampler(0, samplerStateStored);
        }

        //--------------------------------------------------------------------------------------

        private void BeginText11()
        {
            fontVertices.Clear();
        }

        private void DrawText11(Device device, DeviceContext context, string text, RectangleF screen, Color fontColor, float width, float height, bool center)
        {
            float charTextSizeX = 0.010526315f;
            float glyphSizeX = 15.0f / width;
            float glyphSizeY = 42.0f / height;

            float rectLeft = screen.Left / width;
            float rectTop = 1.0f - screen.Top / height;

            rectLeft = rectLeft * 2.0f - 1.0f;
            rectTop = rectTop * 2.0f - 1.0f;

            int numChars = text.Length;
            if (center)
            {
                float rectRight = screen.Right / width;
                rectRight = rectRight * 2.0f - 1.0f;
                float rectBottom = 1.0f - screen.Bottom / height;
                rectBottom = rectBottom * 2.0f - 1.0f;
                float centerx = ((rectRight - rectLeft) - (float)numChars * glyphSizeX) * 0.5f;
                float centery = ((rectTop - rectBottom) - (float)1 * glyphSizeY) * 0.5f;
                rectLeft += centerx;
                rectTop -= centery;
            }
            float originalLeft = rectLeft;
            float texTop = 0.0f;
            float texBottom = 1.0f;

            float depth = 0.5f;

            foreach(char c in text)
            {
                if (c == '\n')
                {
                    rectLeft = originalLeft;
                    rectTop -= glyphSizeY;

                    continue;
                }
                else if (c < 32 || c > 126)
                {
                    continue;
                }

                // Add 6 sprite vertices
                SpriteVertex spriteVertex = new SpriteVertex();
                float rectRight = rectLeft + glyphSizeX;
                float rectBottom = rectTop - glyphSizeY;
                float texLeft = (c - 32) * charTextSizeX;
                float texRight = texLeft + charTextSizeX;

                // tri1
                spriteVertex.Pos = new Vector3(rectLeft, rectTop, depth);
                spriteVertex.Tex = new Vector2(texLeft, texTop);
                spriteVertex.Dif = fontColor.ToVector4();
                fontVertices.Add(spriteVertex);

                spriteVertex.Pos = new Vector3(rectRight, rectTop, depth);
                spriteVertex.Tex = new Vector2(texRight, texTop);
                spriteVertex.Dif = fontColor.ToVector4();
                fontVertices.Add(spriteVertex);

                spriteVertex.Pos = new Vector3(rectLeft, rectBottom, depth);
                spriteVertex.Tex = new Vector2(texLeft, texBottom);
                spriteVertex.Dif = fontColor.ToVector4();
                fontVertices.Add(spriteVertex);

                // tri2
                spriteVertex.Pos = new Vector3(rectRight, rectTop, depth);
                spriteVertex.Tex = new Vector2(texRight, texTop);
                spriteVertex.Dif = fontColor.ToVector4();
                fontVertices.Add(spriteVertex);

                spriteVertex.Pos = new Vector3(rectRight, rectBottom, depth);
                spriteVertex.Tex = new Vector2(texRight, texBottom);
                spriteVertex.Dif = fontColor.ToVector4();
                fontVertices.Add(spriteVertex);

                spriteVertex.Pos = new Vector3(rectLeft, rectBottom, depth);
                spriteVertex.Tex = new Vector2(texLeft, texBottom);
                spriteVertex.Dif = fontColor.ToVector4();
                fontVertices.Add(spriteVertex);

                rectLeft += glyphSizeX;
            }
            EndText(device, context);
        }

        private void EndText(Device device, DeviceContext context)
        {
            int fontDataBytes = Utilities.SizeOf<SpriteVertex>() * fontVertices.Count;
            if (fontBufferBytes11 < fontDataBytes)
            {
                if (fontBuffer11 != null && !fontBuffer11.IsDisposed)
                fontBuffer11.Dispose();
                fontBufferBytes11 = fontDataBytes;

                BufferDescription bufferDesc = new BufferDescription()
                {
                    SizeInBytes = fontBufferBytes11,
                    Usage = ResourceUsage.Dynamic,
                    BindFlags = BindFlags.VertexBuffer,
                    CpuAccessFlags = CpuAccessFlags.Write,
                    OptionFlags = ResourceOptionFlags.None
                };

                fontBuffer11 = new Buffer(device, bufferDesc);
            }

            DataStream mappedResource;
            context.MapSubresource(fontBuffer11, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out mappedResource);
            if (mappedResource!=null)
            {
                mappedResource.WriteRange(fontVertices.ToArray());
                context.UnmapSubresource(fontBuffer11, 0);
            }

            context.Draw(fontVertices.Count, 0);

            fontVertices.Clear();
        }

        public void Dispose()
        {
            Unload();
        }
    }
}
