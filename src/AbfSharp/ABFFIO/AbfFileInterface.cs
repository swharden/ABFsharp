﻿using System.Runtime.InteropServices;

namespace AbfSharp.ABFFIO;

/// <summary>
/// This class provides .NET methods which call ABFFIO.DLL functions under the hood.
/// It opens the ABF in a locked file configuration until it is disposed.
/// </summary>
public class AbfFileInterface : IDisposable
{
    private readonly UInt32 EpisodeCount;
    public readonly string FilePath;
    private readonly float[] SweepBuffer;
    private AbfFileHeader Header;

    public AbfFileInterface(string abfFilePath)
    {
        if (!System.IO.File.Exists(abfFilePath))
            throw new ArgumentException($"file does not exist: {abfFilePath}");

        FilePath = System.IO.Path.GetFullPath(abfFilePath);

        // ensure this file is a valid ABF
        Int32 dataFormat = 0;
        Int32 errorCode = 0;
        ABF_IsABFFile(abfFilePath, ref dataFormat, ref errorCode);
        if (errorCode != 0)
            throw new InvalidOperationException($"ABF_IsABFFile() returned error {errorCode} ({(Error)errorCode})");

        // open the file and read its header
        Int32 fileHandle = 0;
        UInt32 maxSweepLength = 0;
        uint loadFlags = 0;
        ABF_ReadOpen(abfFilePath, ref fileHandle, loadFlags, ref Header, ref maxSweepLength, ref EpisodeCount, ref errorCode);
        if (errorCode != 0)
            throw new InvalidOperationException($"ABF_ReadOpen() returned error {errorCode} ({(Error)errorCode})");

        // create the sweep buffer in memory
        SweepBuffer = new float[maxSweepLength];
    }

    public void Dispose()
    {
        Int32 fileHandle = 0;
        Int32 errorCode = 0;
        ABF_Close(fileHandle, ref errorCode);
        if (errorCode != 0)
            throw new InvalidOperationException($"ABF_Close() returned error {errorCode} ({(Error)errorCode})");
    }

    public AbfFileHeader GetHeader() => Header;

    [DllImport("ABFFIO.dll", CharSet = CharSet.Ansi)]
    private static extern bool ABF_IsABFFile(String szFileName, ref Int32 pnDataFormat, ref Int32 pnError);

    [DllImport("ABFFIO.dll", CharSet = CharSet.Ansi)]
    private static extern bool ABF_ReadOpen(String szFileName, ref Int32 phFile, UInt32 fFlags, ref AbfFileHeader pFH, ref UInt32 puMaxSamples, ref UInt32 pdwMaxEpi, ref Int32 pnError);

    [DllImport("ABFFIO.dll", CharSet = CharSet.Ansi)]
    private static extern bool ABF_Close(Int32 nFile, ref Int32 pnError);

    [DllImport("ABFFIO.dll", CharSet = CharSet.Ansi)]
    private static extern bool ABF_ReadTags(Int32 nFile, ref AbfFileHeader pFH, UInt32 dwFirstTag, ref TagStruct pTagArray, UInt32 uNumTags, ref Int32 pnError);

    /// <summary>
    /// Return tags in the ABF (or an empty array)
    /// </summary>
    public TagStruct[] ReadTags()
    {
        Int32 fileHandle = 0;
        Int32 errorCode = 0;
        TagStruct[] abfTags = new TagStruct[(UInt32)Header.lNumTagEntries];
        for (uint i = 0; i < abfTags.Length; i++)
        {
            ABF_ReadTags(fileHandle, ref Header, i, ref abfTags[i], 1, ref errorCode);
            if (errorCode != 0)
                throw new InvalidOperationException($"ABF_ReadTags() returned error {errorCode} ({(Error)errorCode})");
        }
        return abfTags;
    }

    [DllImport("ABFFIO.dll", CharSet = CharSet.Ansi)]
    private static extern bool ABF_ReadChannel(Int32 nFile, ref AbfFileHeader pFH, Int32 nChannel, Int32 dwEpisode, ref float pfBuffer, ref UInt32 puNumSamples, ref Int32 pnError);

    /// <summary>
    /// Return a new array filled with values from the given sweep and channel
    /// </summary>
    /// <param name="sweep">sweep number (starts at 1)</param>
    /// <param name="channel">channel index (starts at 0)</param>
    /// <returns></returns>
    public float[] ReadChannel(int sweep, int channel)
    {
        Int32 fileHandle = 0;
        Int32 errorCode = 0;
        UInt32 pointsRead = 0;
        Int32 physicalChannel = Header.nADCSamplingSeq[channel];
        ABF_ReadChannel(fileHandle, ref Header, physicalChannel, sweep, ref SweepBuffer[0], ref pointsRead, ref errorCode);
        if (errorCode != 0)
            throw new InvalidOperationException($"ABF_ReadChannel() returned error {errorCode} ({(Error)errorCode})");
        float[] readValues = new float[pointsRead];
        Array.Copy(SweepBuffer, 0, readValues, 0, pointsRead);
        return readValues;
    }

    [DllImport("ABFFIO.dll", CharSet = CharSet.Ansi)]
    private static extern int ABFH_GetEpochDuration(ref AbfFileHeader pFH, Int32 nChannel, Int32 dwEpisode, Int32 nEpoch);
    public int GetEpochDuration(int channelNumber, int sweepNumber, int epochNumber)
    {
        return ABFH_GetEpochDuration(ref Header, channelNumber, sweepNumber, epochNumber);
    }

    [DllImport("ABFFIO.dll", CharSet = CharSet.Ansi)]
    private static extern float ABFH_GetEpochLevel(ref AbfFileHeader pFH, Int32 nChannel, Int32 dwEpisode, Int32 nEpoch);
    public float GetEpochLevel(int channelNumber, int sweepNumber, int epochNumber)
    {
        return ABFH_GetEpochLevel(ref Header, channelNumber, sweepNumber, epochNumber);
    }

    /// <summary>
    /// Return the bounds (sweep point index) of an epoch in a specific episode. 
    /// Values returned are ZERO relative (not relative to start of sweep)
    /// </summary>
    [DllImport("ABFFIO.dll", CharSet = CharSet.Ansi)]
    private static extern bool ABFH_GetEpochLimits(ref AbfFileHeader pFH,
        Int32 nADCChannel, Int32 uDACChannel, Int32 dwEpisode, Int32 nEpoch,
        ref UInt32 puEpochStart, ref UInt32 puEpochEnd, ref Int32 pnError);
    public (bool valid, int start, int end) GetEpochLimits(int channelNumber, int sweepNumber, int epochNumber)
    {
        UInt32 puEpochStart = 0;
        UInt32 puEpochEnd = 0;
        Int32 pnError = 0;
        bool valid = ABFH_GetEpochLimits(ref Header,
            channelNumber, channelNumber, sweepNumber, epochNumber,
            ref puEpochStart, ref puEpochEnd, ref pnError);

        return (valid, (int)puEpochStart, (int)puEpochEnd);
    }

    [DllImport("ABFFIO.dll", CharSet = CharSet.Ansi)]
    private static extern float ABF_GetWaveform(Int32 fileHandle, ref AbfFileHeader pFH, Int32 nChannel, Int32 sweep, ref float pfBuffer, ref Int32 pnError);
    public float[] GetStimulusWaveform(int sweepNumber, int channelIndex)
    {
        Int32 fileHandle = 0;
        Int32 pnError = 0;
        ABF_GetWaveform(fileHandle, ref Header, channelIndex, sweepNumber, ref SweepBuffer[0], ref pnError);

        // shift the waveform forward in time by 1/64th of the sweep length
        float[] values = new float[SweepBuffer.Length];
        Int32 numSamples = Header.lNumSamplesPerEpisode / Header.nADCNumChannels;
        int offset = numSamples / Constants.ABFH_HOLDINGFRACTION;
        Array.Copy(SweepBuffer, 0, values, offset, values.Length - offset);

        if (Header.nInterEpisodeLevel[0] == 0 || sweepNumber == 1)
        {
            // fill the pre-waveform period with the holding command values
            for (int i = 0; i < offset; i++)
                values[i] = Header.fDACHoldingLevel[channelIndex];
        }
        else
        {
            // fill the pre-waveform period with the holding command values from the previous sweep
            ABF_GetWaveform(fileHandle, ref Header, channelIndex, sweepNumber - 1, ref SweepBuffer[0], ref pnError);
            float lastValueOfPreviousSweep = SweepBuffer[SweepBuffer.Length - 1];
            for (int i = 0; i < offset; i++)
                values[i] = lastValueOfPreviousSweep;
        }

        return values;
    }
}
