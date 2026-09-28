"""Align the lower Tsubaki hairpin's contact ribbon with its rendered rails.

The Stage 8 road paths are driving limits, not a collision mesh. At this
hairpin one path cuts 2.31 m inside the rail and the other is 1.51 m behind it.
Only this surveyed section is adapted. Authored road heights, centre path,
race markers and scenery remain unchanged.
"""
import json
import math
import struct

FIRST, LAST = 3520, 3640


def rail_segments(root):
    """Extract actual beam edges, excluding sponsors sharing the same atlas."""
    manifest = json.loads((root / 'scene.json').read_text())
    # Night scenery batches part of the same beams into O_barricade_panel.
    materials = {i for i, m in enumerate(manifest['materials'])
                 if m['name'] in ('E_grail_ref_w6', 'O_barricade_panel')}
    b = (root / 'scene.bin').read_bytes()
    assert b[:4] == b'HKN2'
    at, segments = 8, {}
    for shape in range(struct.unpack_from('<I', b, 4)[0]):
        mat, nv, ni, *matrix = struct.unpack_from('<III12f', b, at)
        at += 60
        if mat not in materials:
            at += nv * 44 + ni * 4
            continue
        vertices = []
        for i in range(nv):
            v = struct.unpack_from('<10f4B', b, at + i * 44)
            p = tuple(sum(matrix[k * 4 + j] * v[j] for j in range(3))
                      + matrix[k * 4 + 3] for k in range(3))
            vertices.append((p, v[6:8]))
        at += nv * 44
        indices = struct.unpack_from('<' + 'I' * ni, b, at)
        at += ni * 4
        for i in range(0, ni, 3):
            triangle = [vertices[indices[i + j]] for j in range(3)]
            # The white guardrail occupies the top-right strip of the atlas.
            if not all(.499 <= uv[0] <= 1.001 and -.001 <= uv[1] <= .063
                       and 580 < p[0] < 710 and 300 < p[2] < 525
                       for p, uv in triangle):
                continue
            for j in range(3):
                a, c = triangle[j][0], triangle[(j + 1) % 3][0]
                length = math.hypot(c[0] - a[0], c[2] - a[2])
                if length < .3 or abs(c[1] - a[1]) > length * .4:
                    continue
                key = tuple(sorted(tuple(round(v, 5) for v in p) for p in (a, c)))
                segments[key] = (a, c, shape, i // 3)
    assert len(segments) > 1000, 'Tsubaki guardrail geometry missing'
    return list(segments.values())


def align_guardrails(root, roads):
    """Return corrected race edges and a denser, endpoint-aware contact ribbon."""
    assert len(roads[0]) == 4112, 'Re-survey the guardrails if the route changes'
    segments = rail_segments(root)

    def sample(index):
        i = min(int(index), len(roads[0]) - 2)
        t = index - i
        return [tuple(a + (b - a) * t for a, b in zip(side[i], side[i + 1]))
                for side in roads]

    cache = {}

    def corrected(index):
        if index in cache:
            return cache[index]
        points, hits = sample(index), [None, None, None]
        if FIRST <= index <= LAST:
            center = points[0]
            for side in (1, 2):
                edge = points[side]
                dx, dy, dz = (edge[k] - center[k] for k in range(3))
                candidates = []
                for a, b, shape, triangle in segments:
                    ex, ez = b[0] - a[0], b[2] - a[2]
                    det = dx * ez - dz * ex
                    if abs(det) < 1e-8:
                        continue
                    ax, az = a[0] - center[0], a[2] - center[2]
                    t, u = (ax * ez - az * ex) / det, (ax * dz - az * dx) / det
                    if not (.3 < t < 1.8 and -.0001 <= u <= 1.0001):
                        continue
                    beam_y = a[1] + u * (b[1] - a[1])
                    if -.2 < beam_y - (center[1] + dy * t) < 1.8:
                        candidates.append((t, shape, triangle))
                if candidates:
                    # First solid beam approached from the road, including its
                    # protruding corrugation, rather than its rear surface.
                    t, shape, triangle = min(candidates)
                    points[side] = (center[0] + dx * t, edge[1], center[2] + dz * t)
                    hits[side] = dict(shape=shape, triangle=triangle,
                                      offset=(t - 1) * math.hypot(dx, dz))
        cache[index] = points, hits
        return points, hits

    indices = set(range(len(roads[0])))
    # Quarter-metre cells follow bends in the beam instead of the coarser
    # driving path. Keep the signed 16-bit RCL vertex/triangle limits intact.
    indices.update(i / 8 for i in range(FIRST * 8, LAST * 8 + 1))
    ordered = sorted(indices)
    for lo, hi in zip(ordered, ordered[1:]):
        if lo < FIRST or hi > LAST:
            continue
        for side in (1, 2):
            present = corrected(lo)[1][side] is not None
            if present == (corrected(hi)[1][side] is not None):
                continue
            a, b = lo, hi
            for _ in range(18):
                mid = (a + b) / 2
                if (corrected(mid)[1][side] is not None) == present:
                    a = mid
                else:
                    b = mid
            # Preserve the actual nose/end of the rail. Do not spread a 1.5 m
            # width change along a full 2 m cell, creating another false wall.
            end = (a + b) / 2
            indices.update((max(lo, end - .0025), min(hi, end + .0025)))
    corrected_roads = [[corrected(i)[0][s] for i in range(len(roads[0]))]
                       for s in range(3)]
    indices = sorted(indices)
    collision_roads = [[corrected(i)[0][s] for i in indices] for s in range(3)]
    report = [dict(point=i, side=s, **hit) for i in indices for s in (1, 2)
              if (hit := corrected(i)[1][s]) is not None]
    assert max(row['offset'] for row in report) > 2.2
    assert min(row['offset'] for row in report) < -1.4
    return corrected_roads, collision_roads, report
