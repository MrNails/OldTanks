using System.Buffers;
using System.Globalization;
using System.Text;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Primitives;
using CoolEngine.Services.Interfaces;
using CoolEngine.Services.Misc;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Loaders;

public class WaveFrontLoader : IModelLoader
{
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

    public async Task<LoaderData> LoadAsync(string path)
    {
        var fInfo = new FileInfo(path);
        var fSizeInKB = (int)(fInfo.Length / 1024 < 1 ? 1 : fInfo.Length / 1024);

        var name = string.Empty;
        var mtlLibName = string.Empty;
        var mtlName = string.Empty;
        var splitSeparator = ' ';

        var scene = new Scene();

        var meshIndex = 0u;
        var lastOffset = new uint[3];
        // var offset = new uint[3];

        var faceData = new List<FaceData>(fSizeInKB < 5 ? 200 : 500);
        var vertices = new List<Vector3>(fSizeInKB < 5 ? 60 : 300);
        var textureCoords = new List<Vector2>(fSizeInKB < 5 ? 60 : 300);
        var normals = new List<Vector3>(fSizeInKB < 5 ? 30 : 200);

        var smoothShading = -1;

        var outFloatValue = 0.0f;
        var outUintValue = 0u;

        var verticesStarted = false;
        var textureStarted = false;
        var normalsStarted = false;
        var facesStarted = false;

        var objectReadError = string.Empty;

        var tempArray = ArrayPool<float>.Shared.Rent(10);

        using (var reader = new StreamReader(path, Encoding.UTF8))
        {
            for (int currLine = 1; !reader.EndOfStream; currLine++)
            {
                if (objectReadError != string.Empty)
                    break;

                var line = await reader.ReadLineAsync();

                if (line.StartsWith('#'))
                    continue;
                if (line.StartsWith("mtllib"))
                    mtlLibName = line.Split(splitSeparator)[1];
                if (line.StartsWith("o"))
                    name = line.Split(splitSeparator)[1];
                if (line.StartsWith("s") && int.TryParse(line.Split(splitSeparator)[1], out int iSmoothRes))
                    smoothShading = iSmoothRes;
                if (line.StartsWith("usemtl"))
                    mtlName = line.Split(splitSeparator)[1];

                if (line.StartsWith("vt"))
                {
                    if (!textureStarted)
                        textureStarted = true;

                    var textureIndices = line.Split(splitSeparator);

                    for (int i = 1, j = 4; i < textureIndices.Length; i++, j++)
                        if (float.TryParse(textureIndices[i], NumberStyles.Float, CultureInfo.InvariantCulture, out outFloatValue))
                            tempArray[j] = outFloatValue;
                        else
                            objectReadError = $"Cannot parse texture coord {i} float in line {currLine}";

                    textureCoords.Add(new Vector2(tempArray[4], tempArray[5]));

                    // offset[1]++;
                }
                else if (line.StartsWith("vn"))
                {
                    if (!normalsStarted)
                        normalsStarted = true;

                    var normal = line.Split(splitSeparator);

                    for (int i = 1, j = 7; i < normal.Length; i++, j++)
                        if (float.TryParse(normal[i], NumberStyles.Float, CultureInfo.InvariantCulture, out outFloatValue))
                            tempArray[j] = outFloatValue;
                        else
                            objectReadError = $"Cannot parse normal {i} float in line {currLine}";

                    normals.Add(new Vector3(tempArray[7], tempArray[8], tempArray[9]));

                    // offset[2]++;
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
                        
                        // offset.CopyTo(lastOffset, 0);

                        meshIndex++;
                    }

                    if (!verticesStarted)
                        verticesStarted = true;

                    var vertex = line.Split(splitSeparator);

                    for (int i = 1, j = 0; i < vertex.Length; i++, j++)
                        if (float.TryParse(vertex[i], NumberStyles.Float, CultureInfo.InvariantCulture, out outFloatValue))
                            tempArray[j] = outFloatValue;
                        else
                            objectReadError = $"Cannot parse vertex {i} float in line {currLine}";


                    vertices.Add(new Vector3(tempArray[0], tempArray[1], tempArray[2]));

                    // offset[0]++;
                }

                if (line.StartsWith("f"))
                {
                    if (!facesStarted)
                        facesStarted = true;

                    var fDataLine = line.Split(splitSeparator);
                    var fTmpArr = new uint[3][]
                    {
                        new uint[fDataLine.Length - 1],
                        textureCoords.Count == 0 ? Array.Empty<uint>() : new uint[fDataLine.Length - 1],
                        normals.Count == 0 ? Array.Empty<uint>() : new uint[fDataLine.Length - 1]
                    };

                    if (fDataLine.Length > 5)
                    {
                        objectReadError = "Cannot load model. Supporting faces: Triangle, Quad";
                        break;
                    }

                    for (int i = 1; i < fDataLine.Length; i++)
                    {
                        var fData = fDataLine[i].Split('/');

                        if (fData.Length == 0)
                        {
                            objectReadError = $"Cannot parse vertexIndices {i} in line {currLine}";
                            break;
                        }

                        for (int j = 0; j < fData.Length; j++)
                        {
                            if (j == 1 && fData[j] == string.Empty)
                                continue;
                            else if (!uint.TryParse(fData[j], out outUintValue))
                            {
                                objectReadError = $"Cannot parse index (uint) [{i};{j}] in line {currLine}";
                                break;
                            }
                            else
                                fTmpArr[j][i - 1] = outUintValue - 1 - lastOffset[j];
                        }
                    }

                    faceData.Add(new FaceData(fTmpArr[0], fTmpArr[1], fTmpArr[2]));
                }
            }
        }

        if (objectReadError != string.Empty)
            Console.WriteLine(objectReadError);
        else
            scene.Meshes.Add(CreateMeshFromData(vertices, textureCoords, normals, faceData));

        ArrayPool<float>.Shared.Return(tempArray);

        return new LoaderData(scene, null);
    }

    private Mesh CreateMeshFromData(List<Vector3> vertices, List<Vector2> textureCoords,
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
}