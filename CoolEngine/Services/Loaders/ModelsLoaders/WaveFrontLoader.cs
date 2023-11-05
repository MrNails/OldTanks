using System.Globalization;
using System.Text;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;
using Serilog;

namespace CoolEngine.Services.Loaders.ModelsLoaders;

public sealed class WaveFrontLoader : IAssetLoader
{
    private enum HeaderDataType
    {
        None,
        Name,
        MtlName,
        MtlLibName,
        SmoothShading
    }

    private static readonly char s_splitSeparator = ' ';

    private struct FaceData
    {
        public uint[] VertexIndices;
        public uint[] TextureIndices;
        public uint[] NormalIndices;

        public FaceData(uint[] vertexIndices, uint[] textureIndices, uint[] normalIndices)
        {
            VertexIndices = vertexIndices;
            TextureIndices = textureIndices;
            NormalIndices = normalIndices;
        }
    }

    private readonly ILogger m_logger;

    public WaveFrontLoader(ILogger logger)
    {
        m_logger = logger;
    }

    public async Task LoadAsset(string path)
    {
        var fInfo = new FileInfo(path);
        var fSizeInKb = (int)(fInfo.Length / 1024 < 1 ? 1 : fInfo.Length / 1024);

        var name = string.Empty;

        var scene = new Scene();

        var lastOffset = new uint[3];

        var faceData = new List<FaceData>(fSizeInKb < 5 ? 200 : 500);
        var vertices = new List<Vector3>(fSizeInKb < 5 ? 60 : 300);
        var textureCoords = new List<Vector2>(fSizeInKb < 5 ? 60 : 300);
        var normals = new List<Vector3>(fSizeInKb < 5 ? 30 : 200);

        var verticesStarted = false;
        var textureStarted = false;
        var normalsStarted = false;
        var facesStarted = false;
        var parseError = false;

        var tempArray = new float[3];

        using (var reader = new StreamReader(path, Encoding.UTF8))
        {
            for (int currLine = 1; !reader.EndOfStream; currLine++)
            {
                var line = await reader.ReadLineAsync();

                if (line.StartsWith('#'))
                    continue;
                if (HandleHeader(line, s_splitSeparator, out var handledData, out var dataType))
                {
                    switch (dataType)
                    {
                        case HeaderDataType.Name:
                            name = handledData;
                            break;
                        case HeaderDataType.MtlName:
                            break;
                        case HeaderDataType.MtlLibName:
                            break;
                        case HeaderDataType.SmoothShading:
                            break;
                    }
                }
                else if (line.StartsWith("vt"))
                {
                    if (!textureStarted)
                        textureStarted = true;

                    parseError = !ParseFloatInLine(line, 1, 2, tempArray,
                        (i, j) => m_logger.Error("Cannot parse texture coord {CoordIdx} float in line {CurrentLine}",
                            i, currLine));

                    textureCoords.Add(new Vector2(tempArray[0], tempArray[1]));
                }
                else if (line.StartsWith("vn"))
                {
                    if (!normalsStarted)
                        normalsStarted = true;

                    parseError = !ParseFloatInLine(line, 1, 3, tempArray,
                        (i, j) => m_logger.Error("Cannot parse normal {CoordIdx} float in line {CurrentLine}",
                            i, currLine));

                    normals.Add(new Vector3(tempArray[0], tempArray[1], tempArray[2]));
                }
                else if (line.StartsWith("v"))
                {
                    if (verticesStarted && (textureStarted || normalsStarted || facesStarted))
                    {
                        scene.Meshes.Add(CreateMeshFromData(vertices, textureCoords, normals, faceData));

                        lastOffset[0] += (uint)vertices.Count;
                        lastOffset[1] += (uint)textureCoords.Count;
                        lastOffset[2] += (uint)normals.Count;

                        vertices.Clear();
                        textureCoords.Clear();
                        normals.Clear();
                        faceData.Clear();

                        verticesStarted = false;
                        textureStarted = false;
                        normalsStarted = false;
                        facesStarted = false;
                    }

                    if (!verticesStarted)
                        verticesStarted = true;

                    parseError = !ParseFloatInLine(line, 1, 3, tempArray,
                        (i, j) => m_logger.Error("Cannot parse vertex {CoordIdx} float in line {CurrentLine}",
                            i, currLine));

                    vertices.Add(new Vector3(tempArray[0], tempArray[1], tempArray[2]));
                }
                else if (line.StartsWith("f"))
                {
                    if (!facesStarted)
                        facesStarted = true;

                    var fDataLine = line.Split(s_splitSeparator);
                    var fTmpArr = new uint[][]
                    {
                        new uint[fDataLine.Length - 1],
                        textureCoords.Count == 0 ? Array.Empty<uint>() : new uint[fDataLine.Length - 1],
                        normals.Count == 0 ? Array.Empty<uint>() : new uint[fDataLine.Length - 1]
                    };

                    if (fDataLine.Length > 5)
                    {
                        m_logger.Error("Cannot load model {Name}. Supporting faces: Triangle, Quad", name);
                        break;
                    }

                    for (int i = 1; i < fDataLine.Length; i++)
                    {
                        var fData = fDataLine[i].Split('/');

                        if (fData.Length == 0)
                        {
                            m_logger.Error("Cannot parse vertexIndices {CoordIdx} in line {CurrentLine}", i, currLine);
                            break;
                        }

                        for (int j = 0; j < fData.Length; j++)
                        {
                            if (j == 1 && fData[j] == string.Empty)
                            {
                                continue;
                            }

                            if (!uint.TryParse(fData[j], out var outUintValue))
                            {
                                m_logger.Error("Cannot parse index (uint) [{i};{j}] in line {CurrentLine}", i, j,
                                    currLine);
                                break;
                            }

                            fTmpArr[j][i - 1] = outUintValue - 1 - lastOffset[j];
                        }
                    }

                    faceData.Add(new FaceData(fTmpArr[0], fTmpArr[1], fTmpArr[2]));
                }
            }
        }

        if (!parseError)
        {
            scene.Meshes.Add(CreateMeshFromData(vertices, textureCoords, normals, faceData));
        }

        GlobalCache<Scene>.Default.AddOrUpdateItem(name, scene);
    }

    private static Mesh CreateMeshFromData(List<Vector3> vertices, List<Vector2> textureCoords,
        List<Vector3> normals, List<FaceData> faceDatas)
    {
        var mesh = new Mesh(vertices.ToArray())
        {
            Normals = normals.Count == 0 ? Array.Empty<Vector3>() : normals.ToArray(),
            TextureCoords = textureCoords.Count == 0 ? Array.Empty<Vector2>() : textureCoords.ToArray(),
        };

        for (int i = 0; i < faceDatas.Count; i++)
            mesh.Faces.Add(new Face(faceDatas[i].VertexIndices,
                faceDatas[i].TextureIndices,
                faceDatas[i].NormalIndices));

        return mesh;
    }

    /// <summary>
    /// Parse floats from given string. Items parsing starts from <see cref="startIndex"/> which represent split items by <see cref="s_splitSeparator"/>.
    /// If parse error appear - <see cref="errorHandler"/> invokes. 
    /// </summary>
    /// <param name="line">Original string array</param>
    /// <param name="startIndex">Start index where parsing begin</param>
    /// <param name="length">Amount of parsing entries</param>
    /// <param name="destinationArray">Array that used as parsed values storage</param>
    /// <param name="errorHandler">Error handler with params (<see cref="line"/> index; <see cref="destinationArray"/> index)</param>
    /// <returns>Return true if array parsed successfully; either false</returns>
    private bool ParseFloatInLine(string line, int startIndex, int length, float[] destinationArray,
        Action<int, int> errorHandler)
    {
        var noErrors = true;

        var tokenStartIdx = 0;
        var parsedItemIdx = 0;

        for (int lineIdx = 0; lineIdx < line.Length && parsedItemIdx < startIndex + length; lineIdx++)
        {
            if (line[lineIdx] != s_splitSeparator &&
                lineIdx != line.Length - 1)
            {
                continue;
            }

            if (lineIdx - 1 == tokenStartIdx)
            {
                if (lineIdx > 0 && line[lineIdx - 1] != ' ')
                {
                    parsedItemIdx++;
                }

                tokenStartIdx = lineIdx;
                continue;
            }

            if (parsedItemIdx < startIndex)
            {
                tokenStartIdx = lineIdx;
                parsedItemIdx++;
                continue;
            }

            var token = line.AsSpan(tokenStartIdx, lineIdx - tokenStartIdx);
            var destinationArrayIdx = parsedItemIdx - startIndex;

            if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var outFloatValue))
            {
                destinationArray[destinationArrayIdx] = outFloatValue;
            }
            else
            {
                errorHandler(parsedItemIdx, destinationArrayIdx);
                noErrors = false;
            }

            tokenStartIdx = lineIdx;
            parsedItemIdx++;
        }

        return noErrors;
    }

    /// <summary>
    /// Handle wavefront file header
    /// </summary>
    /// <param name="line">Line to parse</param>
    /// <param name="splitSeparator">Separator</param>
    /// <param name="data">Parsed data</param>
    /// <param name="headerDataType">Parsed data type</param>
    /// <returns>True if handle; either false</returns>
    private bool HandleHeader(string line, char splitSeparator, out string data, out HeaderDataType headerDataType)
    {
        data = string.Empty;
        headerDataType = HeaderDataType.None;

        if (line.StartsWith("mtllib"))
        {
            headerDataType = HeaderDataType.MtlLibName;
        }
        else if (line.StartsWith("o"))
        {
            headerDataType = HeaderDataType.Name;
        }
        else if (line.StartsWith("s"))
        {
            headerDataType = HeaderDataType.SmoothShading;
        }
        else if (line.StartsWith("usemtl"))
        {
            headerDataType = HeaderDataType.MtlName;
        }

        if (headerDataType != HeaderDataType.None)
        {
            data = line.Split(splitSeparator)[1];
        }

        return headerDataType != HeaderDataType.None;
    }
}