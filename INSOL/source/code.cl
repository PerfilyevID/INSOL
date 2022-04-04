float PointInOrOn( float3 P1, float3 P2, float3 A, float3 B )
{
    float3 CP1 = cross(B - A, P1 - A);
    float3 CP2 = cross(B - A, P2 - A);
    return step(0.0f, dot(CP1, CP2));
}

bool PointInTriangle( float3 px, float3 p0, float3 p1, float3 p2 )
{
    return 
        PointInOrOn(px, p0, p1, p2) *
        PointInOrOn(px, p1, p2, p0) *
        PointInOrOn(px, p2, p0, p1);
}

__kernel void computeTriangles(
    __global float* a1,
    __global float* a2,
    __global float* a3,
    __global float* b1,
    __global float* b2,
    __global float* b3,
    __global float* c1,
    __global float* c2,
    __global float* c3,
    __global float* o1,
    __global float* o2,
    __global float* o3,
    __global float* d1,
    __global float* d2,
    __global float* d3,
    __global int* meta,
    __global bool* result)
    {
        int i = get_global_id(0);
        float _x = (float)i / (float)meta[0];
        int x = (int)floor(_x);
        int y = i % meta[0];
        result[i] = y > x;

        /*
        float3 a = (float3)(a1[x], a2[x], a3[x]);
        float3 b = (float3)(b1[x], b2[x], b3[x]);
        float3 c = (float3)(c1[x], c2[x], c3[x]);
        float3 origin = (float3)(o1[y], o2[y], o3[y]);
        float3 direction = (float3)(d1[y], d2[y], d3[y]);
        float3 N = cross(b-a, c-a);
        float3 X = origin + direction * dot(a - origin, N) / dot(direction, N);
        result[i] = PointInTriangle(X, a, b, c);
        */
    }

__kernel void circleIntersection(
    __global float* circleX,
    __global float* circleY,
    __global float* circleR,
    __global float* rayOX,
    __global float* rayOY,
    __global float* rayDX,
    __global float* rayDY,
    __global int* meta,
    __global bool* result)
    {
        int i = get_global_id(0);
        float _x = (float)i / (float)meta[0];
        int x = (int)floor(_x);
        int y = i % meta[0];
        float2 co = (float2)(circleX[x], circleY[x]);
        float2 p0 = (float2)(rayOX[y], rayOY[y]);
        float2 d0 = (float2)(rayDX[y], rayDY[y]);
        float2 d1 = co - d0;
        float angle = atan2(d1.y, d1.x) - atan2(d0.y, d0.x);
        float distance = length(p0 - co);
        float touch = sin(angle) * distance;
        result[i] = touch < circleR[x];
    }