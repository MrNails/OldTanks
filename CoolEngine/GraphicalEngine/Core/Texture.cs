﻿using OpenTK.Graphics.OpenGL4;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CoolEngine.GraphicalEngine.Core;

public class Texture
{
    public int Handle { get; }

    public float Width { get; }
    public float Height { get; }

    private Texture(int handle, float width, float height)
    {
        Handle = handle;
        Width = width;
        Height = height;
    }

    public void Use(TextureUnit unit, TextureTarget textureTarget = TextureTarget.Texture2D)
    {
        GL.ActiveTexture(unit);
        GL.BindTexture(textureTarget, Handle);
    }

    public static Texture CreateTexture(string path)
    {
        int handle = GL.GenTexture();

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, handle);

        var img = Image.Load<Rgba32>(path);
        img.Mutate(x => x.Flip(FlipMode.Vertical));

        var pixelsAmount = 4 * img.Width * img.Height;
        var pixelIndex = 0;
        var pixels = new byte[pixelsAmount];

        for (int i = 0; i < img.Height; i++)
        {
            var row = img.DangerousGetPixelRowMemory(i).Span;

            for (int j = 0; j < img.Width; j++)
            {
                var currPixels = row[j];

                pixels[pixelIndex++] = currPixels.R;
                pixels[pixelIndex++] = currPixels.G;
                pixels[pixelIndex++] = currPixels.B;
                pixels[pixelIndex++] = currPixels.A;
            }
        }

        GL.TexImage2D(TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba,
            img.Width,
            img.Height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            pixels);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

        img.Dispose();

        return new Texture(handle, img.Width, img.Height);
    }

    public static Texture CreateSkyBoxTextureFromOneImg(string path)
    {
        int handle = GL.GenTexture();

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, handle);

        var img = Image.Load<Rgba32>(path);
        img.Mutate(x => x.Flip(FlipMode.Vertical));

        var parts = new List<KeyValuePair<int, Rgba32[]>>()
        {
            new KeyValuePair<int, Rgba32[]>(2, Array.Empty<Rgba32>()),
            new KeyValuePair<int, Rgba32[]>(1, Array.Empty<Rgba32>()),
            new KeyValuePair<int, Rgba32[]>(5, Array.Empty<Rgba32>()),
            new KeyValuePair<int, Rgba32[]>(0, Array.Empty<Rgba32>()),
            new KeyValuePair<int, Rgba32[]>(4, Array.Empty<Rgba32>()),
            new KeyValuePair<int, Rgba32[]>(3, Array.Empty<Rgba32>())
        };
        var partsIndex = 0;

        var blockWidth = 0;
        var blockHeight = 0;
        var repeatWidthCount = 0;
        var tempBlockHeight = 0;

        var startWidthIndex = 0;

        var block = new List<Rgba32>(img.Width * img.Height / 3);

        for (int i = 0; i < img.Height; i++)
        {
            var row = img.DangerousGetPixelRowMemory(i).Span;
            var tempBlockWidth = 0;

            if (row[0].A == 0)
            {
                tempBlockHeight++;
                bool wasImage = false;

                for (int j = blockWidth; j < img.Width; j++)
                {
                    var currPixels = row[j];

                    if (currPixels.A == 0)
                    {
                        if (wasImage)
                            break;

                        continue;
                    }

                    wasImage = true;

                    tempBlockWidth++;

                    block.Add(currPixels);
                }
            }
            else
            {
                parts[partsIndex] = new KeyValuePair<int, Rgba32[]>(parts[partsIndex].Key, block.ToArray());
                partsIndex++;

                block.Clear();

                blockHeight = tempBlockHeight;
                tempBlockHeight = -1;

                int currentWidthOffset = 0;
                for (int _try = 0; _try < img.Width / blockWidth; _try++)
                {
                    for (int j = i; j < i + blockHeight; j++)
                    {
                        for (int k = currentWidthOffset; k < currentWidthOffset + blockWidth; k++)
                            block.Add(img[k, j]);
                    }

                    parts[partsIndex] = new KeyValuePair<int, Rgba32[]>(parts[partsIndex].Key, block.ToArray());
                    partsIndex++;

                    block.Clear();
                    currentWidthOffset += blockWidth;
                }

                i += blockHeight;
            }

            if (repeatWidthCount != -1 && blockWidth != 0 && tempBlockWidth == blockWidth)
                repeatWidthCount++;
            else
            {
                blockWidth = tempBlockWidth;
                repeatWidthCount = 0;
            }

            if (repeatWidthCount > 2)
                repeatWidthCount = -1;

            tempBlockWidth = 0;
        }

        parts[partsIndex] = new KeyValuePair<int, Rgba32[]>(parts[partsIndex].Key, block.ToArray());

        int idx = 0;
        foreach (var part in parts.OrderBy(p => p.Key))
        {
            GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + idx++,
                0,
                PixelInternalFormat.Rgba,
                blockWidth,
                blockHeight,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                part.Value);
        }

        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter,
            (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Linear);

        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS,
            (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT,
            (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR,
            (int)TextureWrapMode.ClampToEdge);

        // GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

        img.Dispose();

        return new Texture(handle, img.Width, img.Height);
    }

    public static Texture CreateSkyBoxTexture(string dirPath)
    {
        int handle = GL.GenTexture();

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.TextureCubeMap, handle);

        string[] parts = { "right.jpg", "left.jpg", "top.jpg", "bottom.jpg", "front.jpg", "back.jpg" };

        int idx = 0;
        foreach (var part in parts)
        {
            var img = Image.Load<Rgba32>(Path.Combine(dirPath, part));

            var pixles = new Rgba32[img.Width * img.Height];

            img.CopyPixelDataTo(pixles);

            GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + idx++,
                0,
                PixelInternalFormat.Rgba,
                img.Width,
                img.Height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                pixles);

            img.Dispose();
        }

        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter,
            (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Linear);

        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS,
            (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT,
            (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR,
            (int)TextureWrapMode.ClampToEdge);

        return new Texture(handle, -1, -1);
    }

    internal static Texture CreateFontTexture(byte[] img, int width, int height, TextureWrapMode wrapMode)
    {
        var handle = GL.GenTexture();

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, handle);

        GL.TexImage2D(TextureTarget.Texture2D,
            0,
            PixelInternalFormat.CompressedRed,
            width,
            height,
            0,
            PixelFormat.Red,
            PixelType.UnsignedByte,
            img);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Linear);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapMode);

        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

        return new Texture(handle, width, height);
    }
}