#ifndef SDFS_2D_INCLUDED
#define SDFS_2D_INCLUDED

// From https://iquilezles.org/articles/distfunctions2d/

void Circle_float(float2 p, float r, out float Out)
{
    Out = length(p) - r;
}

void RoundedBox_float(float2 p, float2 b, float4 r, out float Out)
{
    r.xy = (p.x > 0.0) ? r.xy : r.zw;
    r.x = (p.y > 0.0) ? r.x : r.y;
    float2 q = abs(p) - b + r.x;
    Out = min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
}


void Trapezoid_float(float2 p, float r1, float r2, float he, out float Out)
{
    float2 k1 = float2(r2, he);
    float2 k2 = float2(r2 - r1, 2.0 * he);
    p.x = abs(p.x);
    float2 ca = float2(p.x - min(p.x, (p.y < 0.0) ? r1 : r2), abs(p.y) - he);
    float2 cb = p - k1 + k2 * clamp(dot(k1 - p, k2) / dot(k2, k2), 0.0, 1.0);
    float s = (cb.x < 0.0 && ca.y < 0.0) ? -1.0 : 1.0;
    Out = s * sqrt(min(dot(ca, ca), dot(cb, cb)));
}

void Triangle_float(float2 p, float r, out float Out)
{
    const float k = sqrt(3.0);
    p.x = abs(p.x) - r;
    p.y = p.y + r / k;
    if (p.x + k * p.y > 0.0)
        p = float2(p.x - k * p.y, -k * p.x - p.y) / 2.0;
    p.x -= clamp(p.x, -2.0 * r, 0.0);
    Out = -length(p) * sign(p.y);
}

void UnevenCapsule_float(float2 p, float r1, float r2, float h, out float Out)
{
    p.x = abs(p.x);
    float b = (r1 - r2) / h;
    float a = sqrt(1.0 - b * b);
    float k = dot(p, float2(-b, a));
    if (k < 0.0)
        Out = length(p) - r1;
    else if (k > a * h)
        Out = length(p - float2(0.0, h)) - r2;
    else
        Out = dot(p, float2(a, b)) - r1;
}

void Hexagon_float(float2 p, float r, out float Out)
{
    const float3 k = float3(-0.866025404, 0.5, 0.577350269);
    p = abs(p);
    p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
    p -= float2(clamp(p.x, -k.z * r, k.z * r), r);
    Out = length(p) * sign(p.y);
}

void Star_float(float2 p, float r, float rf, out float Out)
{
    const float2 k1 = float2(0.809016994375, -0.587785252292);
    const float2 k2 = float2(-k1.x, k1.y);
    p.x = abs(p.x);
    p -= 2.0 * max(dot(k1, p), 0.0) * k1;
    p -= 2.0 * max(dot(k2, p), 0.0) * k2;
    p.x = abs(p.x);
    p.y -= r;
    float2 ba = rf * float2(-k1.y, k1.x) - float2(0, 1);
    float h = clamp(dot(p, ba) / dot(ba, ba), 0.0, r);
    Out = length(p - ba * h) * sign(p.y * ba.x - p.x * ba.y);
}

void Heart_float(float2 p, out float Out)
{

    p.x = abs(p.x);

    if (p.y + p.x > 1.0)
        Out = sqrt(dot(p - float2(0.25, 0.75), p - float2(0.25, 0.75))) - sqrt(2.0) / 4.0;
    else
        Out = sqrt(min(dot(p - float2(0.00, 1.00), p - float2(0.00, 1.00)),
                        dot(p - 0.5 * max(p.x + p.y, 0.0), p - 0.5 * max(p.x + p.y, 0.0)))) * sign(p.x - p.y);
}

void Cross_float(float2 p, float2 b, float r, out float Out)
{
    p = abs(p);
    p = (p.y > p.x) ? p.yx : p.xy;
    float2 q = p - b;
    float k = max(q.y, q.x);
    float2 w = (k > 0.0) ? q : float2(b.y - p.x, -k);
    Out = sign(k) * length(max(w, 0.0)) + r;
}

void RoundedX_float(float2 p, float w, float r, out float Out)
{
    p = abs(p);
    Out = length(p - min(p.x + p.y, w) * 0.5) - r;
}


#endif