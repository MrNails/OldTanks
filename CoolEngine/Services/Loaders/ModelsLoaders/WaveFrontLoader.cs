using System.Globalization;
using System.Text;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services.Extensions;
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

    private static readonly int[] s_quadIndices = new int[] { 0, 1, 3, 1, 2, 3 };
    private static readonly char s_splitSeparator = ' ';

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
        
        var lastOffset = new uint[3];

        var faceData = new List<Face>(fSizeInKb < 5 ? 200 : 500);
        var vertices = new List<Vector3>(fSizeInKb < 5 ? 60 : 300);
        var textureCoords = new List<Vector2>(fSizeInKb < 5 ? 60 : 300);
        var normals = new List<Vector3>(fSizeInKb < 5 ? 30 : 200);

        var verticesStarted = false;
        var textureStarted = false;
        var normalsStarted = false;
        var facesStarted = false;
        var parseError = false;

        var tempArray = new float[3];
        var meshes = new List<Mesh>();
        
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
                        meshes.Add(CreateMeshFromData(vertices, textureCoords, normals, faceData));

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

                    var faceDataLine = line.Split(s_splitSeparator);
                    var faceLength = faceDataLine.Length - 1;
                    var faceType = (faceLength).GetFaceType();

                    if (faceType == FaceType.Unknown)
                    {
                        m_logger.Error("Cannot load model {Name}. Supporting faces: Triangle, Quad", name);
                        return;
                    }
                    
                    var texturesExists = textureCoords.Count != 0;
                    var normalsExists = normals.Count != 0;
                    var faceTmpArr = new uint[][]
                    {
                        new uint[faceLength],
                        texturesExists ? new uint[faceLength] : Array.Empty<uint>(),
                        normalsExists ? new uint[faceLength] : Array.Empty<uint>(),
                    };
                    
                    parseError = !ParseLineData(faceTmpArr, faceDataLine, currLine, lastOffset);
                    
                    if (parseError)
                        return;
                    
                    if (faceType == FaceType.Triangle)
                        faceData.Add(new Face(faceTmpArr[0], faceTmpArr[1], faceTmpArr[2]));
                    else
                        HandleQuadFace(faceData, texturesExists, normalsExists, faceTmpArr);
                }
            }
        }

        if (!parseError)
        {
            meshes.Add(CreateMeshFromData(vertices, textureCoords, normals, faceData));
        }

        GlobalCache<Scene>.Default.AddOrUpdateItem(name, new Scene(meshes.ToArray()));
    }
    
    private bool ParseLineData(uint[][] faceTmpArr, string[] faceDataLine, int currLine, uint[] lastOffset)
    {
        for (int i = 1; i < faceDataLine.Length; i++)
        {
            var fData = faceDataLine[i].Split('/');

            if (fData.Length == 0)
            {
                m_logger.Error("Cannot parse vertexIndices {CoordIdx} in line {CurrentLine}", i, currLine);
                return false;
            }

            for (int j = 0; j < fData.Length; j++)
            {
                if (fData[j] == string.Empty)
                    continue;

                if (!uint.TryParse(fData[j], out var outUintValue))
                {
                    m_logger.Error("Cannot parse index (uint) [{i};{j}] in line {CurrentLine}", i, j,
                        currLine);
                    return false;
                }

                faceTmpArr[j][i - 1] = outUintValue - 1 - lastOffset[j];
            }
        }

        return true;
    }
    
    private static void HandleQuadFace(List<Face> faceData, bool texturesExists, bool normalsExists, uint[][] faceTmpArr)
    {
        faceData.Add(new Face(new uint[3],
            texturesExists ? new uint[3] : Array.Empty<uint>(),
            normalsExists ? new uint[3] : Array.Empty<uint>()));

        faceData.Add(new Face(new uint[3],
            texturesExists ? new uint[3] : Array.Empty<uint>(),
            normalsExists ? new uint[3] : Array.Empty<uint>()));

        var faceDataStartIndex = faceData.Count - 2;
        for (int i = 0; i < s_quadIndices.Length; i++)
        {
            var faceArrCurrentIndex = i % 3;
            var faceDataCurrentIndex = i / 3;
            
            faceData[faceDataStartIndex + faceDataCurrentIndex].Indices[faceArrCurrentIndex] = faceTmpArr[0][s_quadIndices[i]];

            if (texturesExists)
                faceData[faceDataStartIndex + faceDataCurrentIndex].TextureIndices[faceArrCurrentIndex] = faceTmpArr[1][s_quadIndices[i]];

            if (normalsExists)
                faceData[faceDataStartIndex + faceDataCurrentIndex].NormalsIndices[faceArrCurrentIndex] = faceTmpArr[2][s_quadIndices[i]];
        }
    }
    
    private static Mesh CreateMeshFromData(List<Vector3> vertices, List<Vector2> textureCoords,
        List<Vector3> normals, IReadOnlyList<Face> faceDatas)
    {
        return new Mesh(vertices.ToArray(),
            textureCoords.Count == 0 ? Array.Empty<Vector2>() : textureCoords.ToArray(),
            normals.Count == 0 ? Array.Empty<Vector3>() : normals.ToArray(),
            faceDatas.ToArray());
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