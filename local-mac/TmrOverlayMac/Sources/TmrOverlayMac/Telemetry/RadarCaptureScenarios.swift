import Foundation

struct RadarCaptureScenario {
    static let playbackDurationSeconds: TimeInterval = 10

    let title: String
    let sourceCaptureId: String
    let sourceFrameIndex: Int
    let sourceSessionTimeSeconds: TimeInterval
    let sourceFocusCarIdx: Int
    let sourcePlayerCarIdx: Int
    let referenceCarClass: Int?
    let carLeftRight: Int?
    let sideStatus: String
    let hasCarLeft: Bool
    let hasCarRight: Bool
    let nearbyCars: [LiveProximityCar]
    let multiclassApproaches: [LiveMulticlassApproach]

    var proximity: LiveProximitySnapshot {
        proximity(at: 0)
    }

    func proximity(at elapsedSeconds: TimeInterval) -> LiveProximitySnapshot {
        let cars = animatedCars(at: elapsedSeconds)
        let warningApproaches = multiclassWarningApproaches(from: cars)
        let onTrackCars = cars.filter { $0.onPitRoad != true }

        return LiveProximitySnapshot(
            hasData: carLeftRight != nil || !onTrackCars.isEmpty,
            carLeftRight: carLeftRight,
            referenceCarClass: referenceCarClass,
            referenceCarClassColorHex: classColorHex(referenceCarClass),
            sideStatus: sideStatus,
            hasCarLeft: hasCarLeft,
            hasCarRight: hasCarRight,
            nearbyCars: cars,
            nearestAhead: onTrackCars.filter { $0.relativeLaps > 0 }.min { $0.relativeLaps < $1.relativeLaps },
            nearestBehind: onTrackCars.filter { $0.relativeLaps < 0 }.max { $0.relativeLaps < $1.relativeLaps },
            multiclassApproaches: warningApproaches,
            strongestMulticlassApproach: warningApproaches.max { $0.urgency < $1.urgency },
            sideOverlapWindowSeconds: 0.22
        )
    }

    func makeSnapshot(
        capturedAtUtc: Date,
        startedAtUtc: Date?,
        sequence: Int,
        playbackElapsedSeconds: TimeInterval = 0
    ) -> LiveTelemetrySnapshot {
        LiveTelemetrySnapshot(
            isConnected: true,
            isCollecting: true,
            sourceId: sourceCaptureId,
            startedAtUtc: startedAtUtc ?? capturedAtUtc,
            lastUpdatedAtUtc: capturedAtUtc,
            sequence: sequence,
            combo: .mockNurburgringMercedesRace,
            latestFrame: nil,
            fuel: .unavailable,
            proximity: proximity(at: playbackElapsedSeconds),
            leaderGap: .unavailable,
            completedStintCount: 0
        )
    }

    private func animatedCars(at elapsedSeconds: TimeInterval) -> [LiveProximityCar] {
        nearbyCars.enumerated()
            .map { index, car in animatedCar(car, index: index, elapsedSeconds: elapsedSeconds) }
            .sorted { abs($0.relativeLaps) < abs($1.relativeLaps) }
    }

    private func animatedCar(
        _ car: LiveProximityCar,
        index: Int,
        elapsedSeconds: TimeInterval
    ) -> LiveProximityCar {
        guard let baseSeconds = car.relativeSeconds,
              baseSeconds.isFinite,
              !isSuspiciousZeroTiming(seconds: baseSeconds, relativeLaps: car.relativeLaps) else {
            return car
        }

        let adjustedSeconds = animatedRelativeSeconds(
            baseSeconds,
            car: car,
            index: index,
            elapsedSeconds: elapsedSeconds
        )
        var output = car
        output.relativeSeconds = adjustedSeconds
        output.relativeLaps = rescale(car.relativeLaps, baseSeconds: baseSeconds, adjustedSeconds: adjustedSeconds)
        output.relativeMeters = car.relativeMeters.map {
            rescale($0, baseSeconds: baseSeconds, adjustedSeconds: adjustedSeconds)
        }
        return output
    }

    private func animatedRelativeSeconds(
        _ baseSeconds: Double,
        car: LiveProximityCar,
        index: Int,
        elapsedSeconds: TimeInterval
    ) -> Double {
        let phase = Self.loopPhase(elapsedSeconds)
        let wave = sin(phase * Double.pi * 2 + Double((car.carIdx + index) % 13) * 0.47)
        let secondaryWave = sin(phase * Double.pi * 4 + Double(car.carIdx % 7) * 0.31)
        let absoluteSeconds = abs(baseSeconds)
        let multiclass = isMulticlass(car)

        if car.onPitRoad == true {
            return baseSeconds
        }

        if multiclass && baseSeconds < -2 && baseSeconds >= -5 {
            return clamp(baseSeconds + wave * 0.28 + secondaryWave * 0.08, minimum: -4.95, maximum: -2.05)
        }

        if absoluteSeconds <= 0.25 {
            return clamp(baseSeconds + wave * 0.05, minimum: -0.22, maximum: 0.22)
        }

        if absoluteSeconds <= 2 {
            let amplitude = min(0.26, max(0.08, absoluteSeconds * 0.13))
            return clamp(baseSeconds + wave * amplitude + secondaryWave * 0.04, minimum: -1.98, maximum: 1.98)
        }

        if absoluteSeconds <= 5 {
            return baseSeconds + wave * 0.18
        }

        return baseSeconds
    }

    private func multiclassWarningApproaches(from cars: [LiveProximityCar]) -> [LiveMulticlassApproach] {
        guard referenceCarClass != nil else {
            return []
        }

        return cars
            .filter { car in
                guard car.onPitRoad != true,
                      isMulticlass(car),
                      let seconds = car.relativeSeconds,
                      seconds.isFinite else {
                    return false
                }

                return seconds < -2 && seconds >= -5
            }
            .map { car in
                let seconds = abs(car.relativeSeconds ?? 5)
                let urgency = 1 - min(max((seconds - 2) / 3, 0), 1)
                return LiveMulticlassApproach(
                    carIdx: car.carIdx,
                    carClass: car.carClass,
                    relativeLaps: car.relativeLaps,
                    relativeSeconds: car.relativeSeconds,
                    closingRateSecondsPerSecond: nil,
                    urgency: max(0.05, urgency)
                )
            }
            .sorted { $0.urgency > $1.urgency }
    }

    private func isMulticlass(_ car: LiveProximityCar) -> Bool {
        guard let referenceCarClass,
              let carClass = car.carClass else {
            return false
        }

        return carClass != referenceCarClass
    }

    private static func loopPhase(_ elapsedSeconds: TimeInterval) -> Double {
        let wrapped = elapsedSeconds.truncatingRemainder(dividingBy: playbackDurationSeconds)
        let positiveWrapped = wrapped >= 0 ? wrapped : wrapped + playbackDurationSeconds
        return positiveWrapped / playbackDurationSeconds
    }

    private func isSuspiciousZeroTiming(seconds: Double, relativeLaps: Double) -> Bool {
        abs(seconds) <= 0.05 && abs(relativeLaps) >= 0.001
    }

    private func rescale(_ value: Double, baseSeconds: Double, adjustedSeconds: Double) -> Double {
        guard abs(baseSeconds) > 0.0001 else {
            return value
        }

        return value * adjustedSeconds / baseSeconds
    }

    private func clamp(_ value: Double, minimum: Double, maximum: Double) -> Double {
        min(max(value, minimum), maximum)
    }

    static let captureExamples: [RadarCaptureScenario] = [
        RadarCaptureScenario(
            title: "Start Stack Two Left",
            sourceCaptureId: "capture-20260426-130334-932",
            sourceFrameIndex: 156457,
            sourceSessionTimeSeconds: 283.883333,
            sourceFocusCarIdx: 15,
            sourcePlayerCarIdx: 15,
            referenceCarClass: 4098,
            carLeftRight: 5,
            sideStatus: "two left",
            hasCarLeft: true,
            hasCarRight: false,
            nearbyCars: [
                car(36, -1.74976885319e-05, -0.0193071365356, -0.440528805554, 24, 24, 4098, false),
                car(33, 9.18973237276e-05, 0.00300000049174, 2.3136437811, 22, 22, 4098, false),
                car(11, -0.000216541811824, -0.246611595154, -5.4517432712, 23, 23, 4098, false),
                car(20, -0.000240471214056, -0.381083488464, -6.05419947356, 42, 42, 4098, false),
                car(26, -0.000277753919363, -0.425363540649, -6.99284377545, 29, 29, 4098, false),
                car(23, 0.000470023602247, 0.477784156799, 11.8335022196, 20, 20, 4098, false),
                car(31, -0.000717850401998, -0.916416168213, -18.0728888609, 34, 34, 4098, false),
                car(18, 0.000729253515601, 0.703288078308, 18.3599782102, 19, 19, 4098, false),
                car(10, -0.000746555626392, -1.00168704987, -18.7955830723, 28, 28, 4098, false),
                car(27, -0.00076318345964, -1.02885437012, -19.2142120533, 27, 27, 4098, false),
                car(22, -0.000789133831859, -1.05953598022, -19.8675490044, 26, 26, 4098, false),
                car(7, 0.000864552333951, 0.708654403687, 21.7663153805, 21, 21, 4098, false),
                car(4, -0.00094085931778, -1.23133850098, -23.6874505281, 37, 37, 4098, false),
                car(2, -0.00103900954127, -1.25235080719, -26.1585198149, 36, 36, 4098, false),
                car(24, -0.00104760564864, -1.30166912079, -26.3749388523, 44, 44, 4098, false),
                car(42, -0.00111147947609, -1.33432579041, -27.9830518819, 30, 30, 4098, false),
                car(37, -0.00114005617797, -1.35673427582, -28.7025103591, 31, 31, 4098, false),
                car(5, -0.00135874189436, -1.60480690002, -34.2082294293, 38, 38, 4098, false),
                car(13, -0.00141008198261, -1.65979194641, -35.500788027, 40, 40, 4098, false),
                car(49, -0.00143554620445, -1.72408485413, -36.1418854617, 33, 33, 4098, false),
                car(48, 0.00145733729005, 1.29195976257, 36.6905065492, 18, 18, 4098, false),
                car(38, 0.00152036175132, 1.21758270264, 38.2772355959, 16, 16, 4098, false),
                car(6, 0.0016466639936, 1.43303489685, 41.4570713684, 17, 17, 4098, false),
                car(1, -0.00165014714003, -1.91656017303, -41.5447644562, 35, 35, 4098, false),
                car(9, 0.00196054577827, 1.6600484848, 49.3594847322, 15, 15, 4098, false),
                car(39, 0.00236386992037, 1.94224643707, 59.5137346633, 13, 13, 4098, false),
                car(29, 0.0024343971163, 1.85998153687, 61.2893555589, 14, 14, 4098, false),
                car(51, -0.00558881089091, -4.65762233734, -140.706138514, 48, 1, 4099, false),
                car(60, -0.00593924336135, -4.82071352005, -149.528766563, 49, 2, 4099, false),
                car(58, -0.0061042625457, -4.89534568787, -153.683355556, 50, 3, 4099, false)
            ],
            multiclassApproaches: [
                approach(51, 4099, -0.00558881089091, -4.65762233734, nil, 0.114125887553),
                approach(60, 4099, -0.00593924336135, -4.82071352005, nil, 0.0597621599833),
                approach(58, 4099, -0.0061042625457, -4.89534568787, nil, 0.05)
            ]
        ),
        RadarCaptureScenario(
            title: "Left Callout Stack",
            sourceCaptureId: "capture-20260426-130334-932",
            sourceFrameIndex: 156869,
            sourceSessionTimeSeconds: 290.750000,
            sourceFocusCarIdx: 15,
            sourcePlayerCarIdx: 15,
            referenceCarClass: 4098,
            carLeftRight: 2,
            sideStatus: "left",
            hasCarLeft: true,
            hasCarRight: false,
            nearbyCars: [
                car(20, 0.000161178410053, nil, 4.05789212286, 42, 42, 4098, false),
                car(43, 0.000461459159851, 0.34268951416, 11.6178803921, 5, 5, 4098, false),
                car(36, 0.000542353838682, 0.402582168579, 13.6545171842, 24, 24, 4098, false),
                car(33, 0.000546008348465, 0.272298812866, 13.7465245843, 22, 22, 4098, false),
                car(10, -0.000746238976717, -0.681316375732, -18.7876109734, 28, 28, 4098, false),
                car(18, 0.00102012231946, 0.72301864624, 25.6830075637, 19, 19, 4098, false),
                car(23, 0.00104304403067, 0.762186050415, 26.2600937337, 20, 20, 4098, false),
                car(31, -0.0011214017868, -0.893377304077, -28.2328599453, 34, 34, 4098, false),
                car(22, -0.00130961462855, -1.11444473267, -32.9713817343, 26, 26, 4098, false),
                car(6, 0.00140069425106, 1.00227546692, 35.2644387424, 17, 17, 4098, false),
                car(7, 0.00142089650035, 0.879772186279, 35.7730586514, 21, 21, 4098, false),
                car(17, 0.00153733417392, 0.954875946045, 38.7045400962, 11, 11, 4098, false),
                car(26, -0.00162294879556, -1.34321403503, -40.8600080565, 29, 29, 4098, false),
                car(27, -0.00163705646992, -1.35356140137, -41.2151885092, 27, 27, 4098, false),
                car(39, 0.00167287141085, 1.1784286499, 42.116879788, 13, 13, 4098, false),
                car(3, 0.0017229244113, 1.21061706543, 43.3770341486, 4, 4, 4098, false),
                car(48, 0.00175395235419, 1.23049926758, 44.1582060501, 18, 18, 4098, false),
                car(4, -0.00181265547872, -1.46506500244, -45.6361393943, 37, 37, 4098, false),
                car(11, -0.00181466713548, -1.34592628479, -45.6867856696, 23, 23, 4098, false),
                car(38, 0.00192304700613, 1.20085525513, 48.4154006451, 16, 16, 4098, false),
                car(2, -0.00204484164715, -1.50030136108, -51.4817512453, 36, 36, 4098, false),
                car(24, -0.00205321982503, -1.57486915588, -51.6926836029, 44, 44, 4098, false),
                car(9, 0.00236493349075, 1.61916732788, 59.5405115366, 15, 15, 4098, false),
                car(37, -0.00247912853956, -1.79438591003, -62.4155317634, 31, 31, 4098, false),
                car(42, -0.00250479206443, -1.82302474976, -63.0616469309, 30, 30, 4098, false),
                car(5, -0.00257325917482, -1.86918640137, -64.785402289, 38, 38, 4098, false),
                car(47, 0.00260239839554, 1.76786804199, 65.5190229654, 9, 9, 4098, false),
                car(29, 0.00275218486786, 1.71884727478, 69.2901071072, 14, 14, 4098, false),
                car(51, -0.00655408762395, -4.52205276489, -165.008331656, 48, 1, 4099, false)
            ],
            multiclassApproaches: [
                approach(51, 4099, -0.00655408762395, -4.52205276489, nil, 0.159315745036)
            ]
        ),
        RadarCaptureScenario(
            title: "Pit Exit Multiclass Stack",
            sourceCaptureId: "capture-20260426-130334-932",
            sourceFrameIndex: 185400,
            sourceSessionTimeSeconds: 766.266667,
            sourceFocusCarIdx: 15,
            sourcePlayerCarIdx: 15,
            referenceCarClass: 4098,
            carLeftRight: 1,
            sideStatus: "clear",
            hasCarLeft: false,
            hasCarRight: false,
            nearbyCars: [
                car(37, 3.56724485755e-05, 0.016991853714, 0.898103834316, 31, 31, 4098, true),
                car(45, -0.000253589823842, -0.102334499359, -6.38447884098, 32, 32, 4098, true),
                car(26, 0.000614219577983, 0.22669839859, 15.4638377831, 29, 29, 4098, true),
                car(49, -0.000833752565086, -0.320333600044, -20.9908880796, 33, 33, 4098, true),
                car(10, 0.000877268845215, 0.312467813492, 22.0864713547, 11, 11, 4098, false),
                car(31, -0.0021059102146, -0.811928927898, -53.0192379267, 34, 34, 4098, false),
                car(18, -0.0025930221891, -1.00055608153, -65.2829638417, 22, 22, 4098, false),
                car(2, -0.00266022473807, -1.02775484324, -66.9748820954, 36, 36, 4098, false),
                car(43, -0.00271628255723, -1.04971033335, -68.3862161739, 13, 13, 4098, false),
                car(7, 0.00292653334327, 1.07146143913, 73.6795740635, 23, 23, 4098, true),
                car(57, 0.00292784417979, 1.12995290756, 73.7125762082, 56, 9, 4099, true),
                car(48, 0.00379602075554, 1.424223423, 95.5701369499, 21, 21, 4098, true),
                car(4, -0.00584055599757, -0.0119999982417, -147.044174017, 37, 37, 4098, false),
                car(20, -0.00989653286524, -0.0170000009239, -249.159070028, 42, 42, 4098, false),
                car(27, -0.010045902105, -0.00200000032783, -252.919649758, 28, 28, 4098, false),
                car(11, -0.013215379091, nil, -332.715670146, 24, 24, 4098, false),
                car(28, -0.0136505526025, -0.0210000015795, -343.671772541, 46, 46, 4098, false),
                car(8, -0.014093951555, nil, -354.834961929, 17, 17, 4098, false),
                car(9, -0.0164278906304, nil, -413.595145668, 19, 19, 4098, false),
                car(36, -0.0173018735368, nil, -435.598888911, 25, 25, 4098, false),
                car(13, -0.0178291958291, -0.015000000596, -448.874965872, 40, 40, 4098, false),
                car(1, -0.0188090961892, -0.0100000016391, -473.545329298, 35, 35, 4098, false),
                car(44, -0.0192712706048, -0.0219999998808, -485.181217255, 47, 47, 4098, false),
                car(21, -0.0201584857423, -0.0179999992251, -507.518100442, 43, 43, 4098, false),
                car(22, -0.023291246267, -0.00100000016391, -586.389732517, 27, 27, 4098, false),
                car(58, -0.0409338634927, -0.0249999985099, -1030.56732084, 50, 3, 4099, false),
                car(25, -0.0413083594758, -0.019999999553, -1039.99578151, 45, 45, 4098, false),
                car(50, -0.049406246515, -0.0269999988377, -1243.87142476, 52, 5, 4099, false),
                car(51, -0.0521465104539, -0.0229999981821, -1312.86140579, 48, 1, 4099, false),
                car(12, -0.0617375534493, -0.0139999985695, -1554.32934066, 39, 39, 4098, false),
                car(19, -0.072760359617, -0.0159999988973, -1831.84391786, 41, 41, 4098, false),
                car(42, -0.0728215735871, -0.00499999895692, -1833.38506526, 30, 30, 4098, false),
                car(46, -0.0739034574945, -0.0329999998212, -1860.62300726, 58, 49, 4098, false),
                car(55, -0.0914255541284, -0.0300000011921, -2301.76632096, 55, 8, 4099, false),
                car(60, -0.110111967893, -0.0240000002086, -2772.22294847, 49, 2, 4099, false),
                car(54, -0.110120789381, -0.0289999991655, -2772.44504177, 54, 7, 4099, false),
                car(52, -0.113219158025, -0.0280000008643, -2850.45081011, 53, 6, 4099, false),
                car(35, -0.11620177445, -0.0320000015199, -2925.54235426, 57, 48, 4098, false),
                car(59, -0.140841380926, -0.0260000005364, -3545.87894274, 51, 4, 4099, false)
            ],
            multiclassApproaches: [
                approach(58, 4099, -0.0409338634927, -0.0249999985099, nil, 1),
                approach(50, 4099, -0.049406246515, -0.0269999988377, nil, 1),
                approach(51, 4099, -0.0521465104539, -0.0229999981821, nil, 1),
                approach(55, 4099, -0.0914255541284, -0.0300000011921, nil, 1),
                approach(60, 4099, -0.110111967893, -0.0240000002086, nil, 1),
                approach(54, 4099, -0.110120789381, -0.0289999991655, nil, 1),
                approach(52, 4099, -0.113219158025, -0.0280000008643, nil, 1),
                approach(59, 4099, -0.140841380926, -0.0260000005364, nil, 1)
            ]
        ),
        RadarCaptureScenario(
            title: "Zero Timing Train",
            sourceCaptureId: "capture-20260426-130334-932",
            sourceFrameIndex: 63551,
            sourceSessionTimeSeconds: 546.450000,
            sourceFocusCarIdx: 15,
            sourcePlayerCarIdx: 15,
            referenceCarClass: 4098,
            carLeftRight: 1,
            sideStatus: "clear",
            hasCarLeft: false,
            hasCarRight: false,
            nearbyCars: [
                car(13, 0.00223566195928, 0.85310536623, 56.2859197515, 0, 0, 4098, false),
                car(2, 0.00359575613402, 1.363922894, 90.5281947325, 0, 0, 4098, false),
                car(42, -0.00703846500255, 0, -177.20321029, 0, 0, 4098, false),
                car(34, -0.00768618867733, 0, -193.510560616, 0, 0, 4098, false),
                car(40, -0.0080972223077, 0, -203.858907708, 0, 0, 4098, false),
                car(43, -0.00893723056652, 0, -225.007291635, 0, 0, 4098, false),
                car(48, -0.0168799667154, 0, -424.976794012, 0, 0, 4098, false),
                car(17, -0.0199823288713, 0, -503.083104595, 0, 0, 4098, false),
                car(10, -0.0231962709222, 0, -583.998595246, 0, 0, 4098, false),
                car(36, -0.0252543597016, 0, -635.813861593, 0, 0, 4098, false),
                car(31, -0.0257756023202, 0, -648.936874255, 0, 0, 4098, false),
                car(41, -0.0308643488679, 0, -777.053192838, 0, 0, 4098, false),
                car(47, -0.0466669111047, 0, -1174.90482074, 0, 0, 4098, false),
                car(14, -0.0559483675752, 0, -1408.57848142, 0, 0, 4098, false),
                car(9, -0.0565627722535, 0, -1424.04697936, 0, 0, 4098, false),
                car(50, -0.0606939701829, 0, -1528.05567091, 0, 0, 4099, false),
                car(28, -0.071563830832, 0, -1801.71963056, 0, 0, 4098, false),
                car(4, -0.0742583780084, 0, -1869.55862809, 0, 0, 4098, false),
                car(23, -0.0834290890489, 0, -2100.44411753, 0, 0, 4098, false),
                car(49, -0.101041605929, 0, -2543.86388751, 0, 0, 4098, false),
                car(33, -0.113363197306, 0, -2854.07720066, 0, 0, 4098, false),
                car(60, -0.120668402174, 0, -3037.99596051, 0, 0, 4099, false),
                car(37, -0.136066546896, 0, -3425.66581128, 0, 0, 4098, false),
                car(16, -0.147328844527, 0, -3709.20992134, 0, 0, 4098, false),
                car(32, -0.156215837458, 0, -3932.95241018, 0, 0, 4098, false),
                car(52, -0.15730278776, 0, -3960.31790577, 0, 0, 4099, false),
                car(55, 0.168911659857, 0, 4252.58751323, 0, 0, 4099, false),
                car(11, -0.216586521128, 0, -5452.86889053, 0, 0, 4098, false),
                car(54, -0.23815189884, 0, -5995.80746597, 0, 0, 4099, false),
                car(18, -0.248064806918, 0, -6245.37880488, 0, 0, 4098, false),
                car(12, 0.300375649473, 0, 7562.37750138, 0, 0, 4098, false),
                car(53, -0.317640831927, 0, -7997.05264092, 0, 0, 4099, false),
                car(44, -0.320813169936, 0, -8076.92069157, 0, 0, 4098, false),
                car(29, -0.324539235095, 0, -8170.72959843, 0, 0, 4098, false),
                car(22, -0.340082695941, 0, -8562.05798608, 0, 0, 4098, false),
                car(45, -0.37106679962, 0, -9342.12617396, 0, 0, 4098, false),
                car(51, -0.377650013426, 0, -9507.86779803, 0, 0, 4099, false),
                car(59, 0.416367271682, 0, 10482.6289788, 0, 0, 4099, false),
                car(58, -0.440108170966, 0, -11080.3393555, 0, 0, 4099, false),
                car(38, -0.491516759852, 0, -12374.6225527, 0, 0, 4098, false)
            ],
            multiclassApproaches: []
        )
    ]
}

private func car(
    _ carIdx: Int,
    _ relativeLaps: Double,
    _ relativeSeconds: Double?,
    _ relativeMeters: Double?,
    _ overallPosition: Int?,
    _ classPosition: Int?,
    _ carClass: Int?,
    _ onPitRoad: Bool?
) -> LiveProximityCar {
    LiveProximityCar(
        carIdx: carIdx,
        relativeLaps: relativeLaps,
        relativeSeconds: relativeSeconds,
        relativeMeters: relativeMeters,
        overallPosition: overallPosition,
        classPosition: classPosition,
        carClass: carClass,
        carClassColorHex: classColorHex(carClass),
        onPitRoad: onPitRoad
    )
}

private func classColorHex(_ carClass: Int?) -> String? {
    switch carClass {
    case 4098:
        return "#FFDA59"
    case 4099:
        return "#33CEFF"
    default:
        return nil
    }
}

private func approach(
    _ carIdx: Int,
    _ carClass: Int?,
    _ relativeLaps: Double,
    _ relativeSeconds: Double?,
    _ closingRateSecondsPerSecond: Double?,
    _ urgency: Double
) -> LiveMulticlassApproach {
    LiveMulticlassApproach(
        carIdx: carIdx,
        carClass: carClass,
        relativeLaps: relativeLaps,
        relativeSeconds: relativeSeconds,
        closingRateSecondsPerSecond: closingRateSecondsPerSecond,
        urgency: urgency
    )
}
