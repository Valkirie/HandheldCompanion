using System;

namespace ControllerCommon;

public class OneEuroFilter
{
    protected double dcutoff;
    protected LowpassFilter dxFilt;

    protected bool firstTime;
    protected OneEuroSettings settings;
    protected LowpassFilter xFilt;

    public OneEuroFilter(double minCutoff, double beta)
    {
        settings = new OneEuroSettings(minCutoff, beta);
        firstTime = true;

        xFilt = new LowpassFilter();
        dxFilt = new LowpassFilter();
        dcutoff = 1;
    }

    public double MinCutoff
    {
        get => settings.minCutoff;
        set => settings.minCutoff = value;
    }

    public double Beta
    {
        get => settings.beta;
        set => settings.beta = value;
    }

    public double Filter(double x, double rate)
    {
        var dx = firstTime ? 0 : (x - xFilt.Last()) * rate;
        if (firstTime) firstTime = false;

        var edx = dxFilt.Filter(dx, Alpha(rate, dcutoff));
        var cutoff = settings.minCutoff + settings.beta * Math.Abs(edx);

        return xFilt.Filter(x, Alpha(rate, cutoff));
    }

    protected double Alpha(double rate, double cutoff)
    {
        var tau = 1.0 / (2 * Math.PI * cutoff);
        var te = 1.0 / rate;
        return 1.0 / (1.0 + tau / te);
    }

    public class OneEuroSettings
    {
        public double beta;
        public double minCutoff;

        public OneEuroSettings(double minCutoff, double beta)
        {
            this.minCutoff = minCutoff;
            this.beta = beta;
        }
    }
}

public class LowpassFilter
{
    protected bool firstTime;
    protected double hatXPrev;

    public LowpassFilter()
    {
        firstTime = true;
    }

    public double Last()
    {
        return hatXPrev;
    }

    public double Filter(double x, double alpha)
    {
        double hatX = 0;
        if (firstTime)
        {
            firstTime = false;
            hatX = x;
        }
        else
        {
            hatX = alpha * x + (1 - alpha) * hatXPrev;
        }

        hatXPrev = hatX;

        return hatX;
    }
}