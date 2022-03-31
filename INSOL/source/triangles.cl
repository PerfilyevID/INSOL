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
    __global float* a1, __global float* a2, __global float* a3,
    __global float* b1, __global float* b2, __global float* b3,
    __global float* c1, __global float* c2, __global float* c3,
    __global float* o1, __global float* o2, __global float* o3,
    __global float* d1, __global float* d2, __global float* d3,
    __global int* result)
    {
        int i = get_global_id(0);
        float3 a = (float3)(a1[i], a2[i], a3[i]);
        float3 b = (float3)(b1[i], b2[i], b3[i]);
        float3 c = (float3)(c1[i], c2[i], c3[i]);
        float3 origin = (float3)(o1[i], o2[i], o3[i]);
        float3 direction = (float3)(d1[i], d2[i], d3[i]);

        float3 N = cross(b-a, c-a);
        float3 X = origin + direction * dot(a - origin, N) / dot(direction, N);
        if(PointInTriangle(X, a, b, c)) {
            result[0] += 1;
        }
        return;
    }